using System;
using System.IO;
using System.Threading.Tasks;

namespace IndustrialProccesingSystemAPI
{
    public class JobEventLogger
    {
        private static readonly object syncRoot = new object();
        private readonly string eventLogPath;

        public JobEventLogger(string eventLogPath)
        {
            this.eventLogPath = eventLogPath ?? throw new ArgumentNullException(nameof(eventLogPath));
            var directory = Path.GetDirectoryName(Path.GetFullPath(this.eventLogPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public Task WriteCompletedAsync(Job job, int result)
        {
            return AppendLineAsync(string.Format("{0:yyyy-MM-dd HH:mm:ss} [COMPLETED] {1} {2}, {3}", DateTime.Now, job.Id, job.Type, result));
        }

        public Task WriteFailedAsync(Job job, Exception exception)
        {
            return AppendLineAsync(string.Format("{0:yyyy-MM-dd HH:mm:ss} [FAILED] {1} {2}, {3}", DateTime.Now, job.Id, job.Type, exception.Message));
        }

        public Task WriteAbortedAsync(Job job)
        {
            return AppendLineAsync(string.Format("{0:yyyy-MM-dd HH:mm:ss} [ABORT] {1} {2}, Ignored", DateTime.Now, job.Id, job.Type));
        }

        private Task AppendLineAsync(string line)
        {
            lock (syncRoot)
            {
                using (var stream = new FileStream(eventLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream))
                {
                    writer.WriteLine(line);
                }
            }

            return Task.CompletedTask;
        }
    }
}
