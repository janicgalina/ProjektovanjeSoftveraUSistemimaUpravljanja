using IndustrialProccesingSystemAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace UnitTestProject
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void JobQueue_ReturnsJobsByPriority()
        {
            var queue = new JobQueue(10);
            var lowPriority = new Job(Guid.NewGuid(), JobType.Prime, "10", 5);
            var highPriority = new Job(Guid.NewGuid(), JobType.IO, "1", 1);
            var middlePriority = new Job(Guid.NewGuid(), JobType.Prime, "20", 3);

            Assert.IsTrue(queue.TryEnqueue(lowPriority));
            Assert.IsTrue(queue.TryEnqueue(highPriority));
            Assert.IsTrue(queue.TryEnqueue(middlePriority));

            Job dequeued;
            Assert.IsTrue(queue.TryDequeue(out dequeued));
            Assert.AreEqual(highPriority.Id, dequeued.Id);

            Assert.IsTrue(queue.TryDequeue(out dequeued));
            Assert.AreEqual(middlePriority.Id, dequeued.Id);

            Assert.IsTrue(queue.TryDequeue(out dequeued));
            Assert.AreEqual(lowPriority.Id, dequeued.Id);
        }

        [TestMethod]
        public void JobProcessor_PrimeJobCountsPrimes()
        {
            var processor = new JobProcessor();
            var job = new Job(Guid.NewGuid(), JobType.Prime, "10", 1);

            var result = processor.ProcessAsync(job).Result;

            Assert.AreEqual(4, result);
        }

        [TestMethod]
        public void JobProcessor_IoJobReturnsValueInRange()
        {
            var processor = new JobProcessor();
            var job = new Job(Guid.NewGuid(), JobType.IO, "anything", 1);

            var result = processor.ProcessAsync(job).Result;

            Assert.IsTrue(result >= 0 && result <= 100);
        }

        [TestMethod]
        public void SystemConfiguration_LoadsConfigurationAndInitialJobs()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
            var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><SystemConfiguration><WorkerCount>2</WorkerCount><MaxQueueSize>7</MaxQueueSize><EventLogPath>log.txt</EventLogPath><ReportDirectory>Reports</ReportDirectory><InitialJobs><Job><Id>11111111-1111-1111-1111-111111111111</Id><Type>Prime</Type><Payload>20</Payload><Priority>1</Priority></Job></InitialJobs></SystemConfiguration>";
            File.WriteAllText(tempFile, xml);

            try
            {
                var configuration = SystemConfiguration.Load(tempFile);

                Assert.AreEqual(2, configuration.WorkerCount);
                Assert.AreEqual(7, configuration.MaxQueueSize);
                Assert.AreEqual(1, configuration.InitialJobs.Count);
                Assert.AreEqual(JobType.Prime, configuration.InitialJobs[0].Type);
                Assert.AreEqual("20", configuration.InitialJobs[0].Payload);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void ProcessingSystem_SubmitReturnsHandleAndCompletes()
        {
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var eventLogPath = Path.Combine(basePath, "event-log.txt");
            var reportDirectory = Path.Combine(basePath, "Reports");
            Directory.CreateDirectory(basePath);

            using (var system = new ProcessingSystem(1, 10, null, eventLogPath, reportDirectory))
            {
                var job = new Job(Guid.NewGuid(), JobType.Prime, "10", 1);
                var handle = system.Submit(job);

                Assert.AreEqual(job.Id, handle.Id);
                Assert.IsTrue(handle.Result.Wait(TimeSpan.FromSeconds(5)));
                Assert.AreEqual(4, handle.Result.Result);
            }
        }

        [TestMethod]
        public void ProcessingSystem_GetJobReturnsSubmittedJob()
        {
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var eventLogPath = Path.Combine(basePath, "event-log.txt");
            var reportDirectory = Path.Combine(basePath, "Reports");
            Directory.CreateDirectory(basePath);

            using (var system = new ProcessingSystem(1, 10, null, eventLogPath, reportDirectory))
            {
                var job = new Job(Guid.NewGuid(), JobType.IO, "anything", 1);
                system.Submit(job);

                var loadedJob = system.GetJob(job.Id);

                Assert.IsNotNull(loadedJob);
                Assert.AreEqual(job.Id, loadedJob.Id);
            }
        }

        [TestMethod]
        public void ProcessingSystem_RejectedDuplicateJobId()
        {
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var eventLogPath = Path.Combine(basePath, "event-log.txt");
            var reportDirectory = Path.Combine(basePath, "Reports");
            Directory.CreateDirectory(basePath);

            using (var system = new ProcessingSystem(1, 10, null, eventLogPath, reportDirectory))
            {
                var jobId = Guid.NewGuid();
                var firstJob = new Job(jobId, JobType.IO, "anything", 1);
                var duplicateJob = new Job(jobId, JobType.IO, "anything", 1);

                system.Submit(firstJob);

                Assert.ThrowsException<InvalidOperationException>(() => system.Submit(duplicateJob));
            }
        }

        [TestMethod]
        public void ProcessingSystem_FailingPrimeJobThrowsAfterRetries()
        {
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var eventLogPath = Path.Combine(basePath, "event-log.txt");
            var reportDirectory = Path.Combine(basePath, "Reports");
            Directory.CreateDirectory(basePath);

            using (var system = new ProcessingSystem(1, 10, null, eventLogPath, reportDirectory))
            {
                var job = new Job(Guid.NewGuid(), JobType.Prime, "not-a-number", 1);
                var handle = system.Submit(job);

                try
                {
                    handle.Result.Wait(TimeSpan.FromSeconds(10));
                    Assert.Fail("Expected the job to fail.");
                }
                catch (AggregateException exception)
                {
                    Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidOperationException));
                }
            }
        }
    }
}
