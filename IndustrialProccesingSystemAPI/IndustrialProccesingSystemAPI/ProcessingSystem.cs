using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IndustrialProccesingSystemAPI
{
    public class ProcessingSystem : IDisposable
    {
        private sealed class PendingJob
        {
            public Job Job { get; }
            public TaskCompletionSource<int> CompletionSource { get; }
            public int Attempts { get; set; }
            public DateTime StartedAtUtc { get; set; }

            public PendingJob(Job job, TaskCompletionSource<int> completionSource)
            {
                Job = job;
                CompletionSource = completionSource;
            }
        }

        private readonly object _syncRoot = new object();
        private readonly Dictionary<Guid, PendingJob> _pendingById = new Dictionary<Guid, PendingJob>();
        private readonly HashSet<Guid> _completedIds = new HashSet<Guid>();
        private readonly HashSet<Guid> _abortedIds = new HashSet<Guid>();
        private readonly Dictionary<JobType, List<int>> _durationsByType = new Dictionary<JobType, List<int>>();
        private readonly Dictionary<JobType, int> _failedByType = new Dictionary<JobType, int>();
        private readonly JobQueue _queue;
        private readonly JobProcessor _processor;
        private readonly JobEventLogger _eventLogger;
        private readonly JobReportService _reportService;
        private readonly List<Task> _workers = new List<Task>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Timer _reportTimer;
        private bool _disposed;

        public event Action<Job, int> JobCompleted;
        public event Action<Job, Exception> JobFailed;

        public ProcessingSystem(int workerCount, int maxQueueSize, IEnumerable<Job> initialJobs, string eventLogPath, string reportDirectory)
        {
            if (workerCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(workerCount));
            }

            _queue = new JobQueue(maxQueueSize);
            _processor = new JobProcessor();
            _eventLogger = new JobEventLogger(eventLogPath);
            _reportService = new JobReportService(reportDirectory);

            JobCompleted += (job, result) => _eventLogger.WriteCompletedAsync(job, result).Wait();
            JobFailed += (job, exception) => _eventLogger.WriteFailedAsync(job, exception).Wait();

            if (initialJobs != null)
            {
                foreach (var job in initialJobs)
                {
                    TrySubmit(job);
                }
            }

            for (var i = 0; i < workerCount; i++)
            {
                _workers.Add(Task.Run(() => WorkerLoop(_cancellationTokenSource.Token)));
            }

            _reportTimer = new Timer(_ => GenerateReport(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public JobHandle Submit(Job job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            var completionSource = new TaskCompletionSource<int>();
            var handle = new JobHandle(job.Id, completionSource);

            lock (_syncRoot)
            {
                if (_pendingById.ContainsKey(job.Id) || _completedIds.Contains(job.Id) || _abortedIds.Contains(job.Id))
                {
                    throw new InvalidOperationException("Job with the same Id has already been processed or queued.");
                }

                if (!_queue.TryEnqueue(job))
                {
                    throw new InvalidOperationException("Queue is full.");
                }

                _pendingById[job.Id] = new PendingJob(job, completionSource);
            }

            return handle;
        }

        public IEnumerable<Job> GetTopJobs(int n)
        {
            return _queue.GetTopJobs(n);
        }

        public Job GetJob(Guid id)
        {
            lock (_syncRoot)
            {
                PendingJob pending;
                return _pendingById.TryGetValue(id, out pending) ? pending.Job : null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancellationTokenSource.Cancel();

            try
            {
                Task.WaitAll(_workers.ToArray(), TimeSpan.FromSeconds(5));
            }
            catch
            {
            }

            _reportTimer.Dispose();
            _cancellationTokenSource.Dispose();
        }

        private void WorkerLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Thread.Sleep(25);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                Job job;
                if (!_queue.TryDequeue(out job))
                {
                    continue;
                }

                PendingJob pending;
                lock (_syncRoot)
                {
                    if (!_pendingById.TryGetValue(job.Id, out pending))
                    {
                        continue;
                    }

                    pending.StartedAtUtc = DateTime.UtcNow;
                    pending.Attempts++;
                }

                ExecuteJob(pending);
            }
        }

        private void ExecuteJob(PendingJob pending)
        {
            var job = pending.Job;

            try
            {
                var result = _processor.ProcessAsync(job).GetAwaiter().GetResult();
                FinishCompleted(pending, result);
            }
            catch (Exception exception)
            {
                FinishFailed(pending, exception);
            }
        }

        private void FinishCompleted(PendingJob pending, int result)
        {
            lock (_syncRoot)
            {
                _completedIds.Add(pending.Job.Id);
                AddDuration(pending.Job.Type, (int)(DateTime.UtcNow - pending.StartedAtUtc).TotalMilliseconds);
                _pendingById.Remove(pending.Job.Id);
            }

            pending.CompletionSource.TrySetResult(result);
            JobCompleted?.Invoke(pending.Job, result);
        }

        private void FinishFailed(PendingJob pending, Exception exception)
        {
            var retry = false;
            var abort = false;

            lock (_syncRoot)
            {
                if (pending.Attempts < 3)
                {
                    retry = true;
                }
                else
                {
                    abort = true;
                    _abortedIds.Add(pending.Job.Id);
                    IncrementFailedCount(pending.Job.Type);
                    _pendingById.Remove(pending.Job.Id);
                }
            }

            JobFailed?.Invoke(pending.Job, exception);

            if (retry)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(200);
                    lock (_syncRoot)
                    {
                        if (!_completedIds.Contains(pending.Job.Id) && !_abortedIds.Contains(pending.Job.Id))
                        {
                            _queue.TryEnqueue(pending.Job);
                        }
                    }
                });
                return;
            }

            if (abort)
            {
                pending.CompletionSource.TrySetException(new InvalidOperationException("Job failed after 3 attempts.", exception));
                _eventLogger.WriteAbortedAsync(pending.Job).Wait();
            }
        }

        private void AddDuration(JobType type, int durationMs)
        {
            List<int> durations;
            if (!_durationsByType.TryGetValue(type, out durations))
            {
                durations = new List<int>();
                _durationsByType[type] = durations;
            }

            durations.Add(Math.Max(0, durationMs));
        }

        private void IncrementFailedCount(JobType type)
        {
            int count;
            if (_failedByType.TryGetValue(type, out count))
            {
                _failedByType[type] = count + 1;
            }
            else
            {
                _failedByType[type] = 1;
            }
        }

        private void GenerateReport()
        {
            List<JobExecutionSnapshot> snapshots;
            lock (_syncRoot)
            {
                snapshots = _durationsByType.Select(pair => new JobExecutionSnapshot(
                    pair.Key,
                    pair.Value.Count,
                    _failedByType.ContainsKey(pair.Key) ? _failedByType[pair.Key] : 0,
                    pair.Value.ToList())).ToList();

                foreach (var pair in _failedByType)
                {
                    if (snapshots.All(item => item.Type != pair.Key))
                    {
                        snapshots.Add(new JobExecutionSnapshot(pair.Key, 0, pair.Value, new List<int>()));
                    }
                }
            }

            _reportService.GenerateReport(snapshots);
        }

        private bool TrySubmit(Job job)
        {
            try
            {
                Submit(job);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
