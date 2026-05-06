using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace UnitTestProject
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void JobConstructor_StoresValuesAndRejectsNullPayload()
        {
            var app = LoadAppAssembly();
            var jobType = app.GetType("IndustrialProccesingSystemAPI.Job");
            var jobEnum = app.GetType("IndustrialProccesingSystemAPI.JobType");

            var id = Guid.NewGuid();

            var exception = Assert.ThrowsException<TargetInvocationException>(() =>
                CreateJob(jobType, jobEnum, "Prime", null, 5));
            Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));

            var job = CreateJob(jobType, jobEnum, "IO", "payload", 3, id);

            Assert.AreEqual(id, (Guid)jobType.GetProperty("Id").GetValue(job));
            Assert.AreEqual(Enum.Parse(jobEnum, "IO"), jobType.GetProperty("Type").GetValue(job));
            Assert.AreEqual("payload", (string)jobType.GetProperty("Payload").GetValue(job));
            Assert.AreEqual(3, (int)jobType.GetProperty("Priority").GetValue(job));
        }

        [TestMethod]
        public void JobQueue_EnqueueDequeueAndTopJobsWorkAsExpected()
        {
            var app = LoadAppAssembly();
            var jobType = app.GetType("IndustrialProccesingSystemAPI.Job");
            var queueType = app.GetType("IndustrialProccesingSystemAPI.JobQueue");
            var jobEnum = app.GetType("IndustrialProccesingSystemAPI.JobType");

            var queue = Activator.CreateInstance(queueType, 3);
            var first = CreateJob(jobType, jobEnum, "Prime", "10", 5);
            var second = CreateJob(jobType, jobEnum, "IO", "x", 1);
            var third = CreateJob(jobType, jobEnum, "Prime", "20", 3);

            Assert.IsTrue((bool)queueType.GetMethod("TryEnqueue").Invoke(queue, new[] { first }));
            Assert.IsTrue((bool)queueType.GetMethod("TryEnqueue").Invoke(queue, new[] { second }));
            Assert.IsTrue((bool)queueType.GetMethod("TryEnqueue").Invoke(queue, new[] { third }));

            var topJobs = ((IEnumerable<object>)queueType.GetMethod("GetTopJobs").Invoke(queue, new object[] { 2 })).ToList();

            Assert.AreEqual(2, topJobs.Count);
            Assert.AreEqual(jobType.GetProperty("Id").GetValue(second), jobType.GetProperty("Id").GetValue(topJobs[0]));
            Assert.AreEqual(jobType.GetProperty("Id").GetValue(third), jobType.GetProperty("Id").GetValue(topJobs[1]));

            var tryDequeue = queueType.GetMethod("TryDequeue");
            var args = new object[] { null };

            Assert.IsTrue((bool)tryDequeue.Invoke(queue, args));
            Assert.AreEqual(jobType.GetProperty("Id").GetValue(second), jobType.GetProperty("Id").GetValue(args[0]));

            args[0] = null;
            Assert.IsTrue((bool)tryDequeue.Invoke(queue, args));
            Assert.AreEqual(jobType.GetProperty("Id").GetValue(third), jobType.GetProperty("Id").GetValue(args[0]));

            args[0] = null;
            Assert.IsTrue((bool)tryDequeue.Invoke(queue, args));
            Assert.AreEqual(jobType.GetProperty("Id").GetValue(first), jobType.GetProperty("Id").GetValue(args[0]));
        }

        [TestMethod]
        public void JobQueue_RejectsInvalidInputs()
        {
            var app = LoadAppAssembly();
            var queueType = app.GetType("IndustrialProccesingSystemAPI.JobQueue");

            var constructorException = Assert.ThrowsException<TargetInvocationException>(() => Activator.CreateInstance(queueType, 0));
            Assert.IsInstanceOfType(constructorException.InnerException, typeof(ArgumentOutOfRangeException));

            var queue = Activator.CreateInstance(queueType, 1);
            var enqueueException = Assert.ThrowsException<TargetInvocationException>(() => queueType.GetMethod("TryEnqueue").Invoke(queue, new object[] { null }));
            Assert.IsInstanceOfType(enqueueException.InnerException, typeof(ArgumentNullException));
        }

        [TestMethod]
        public void SystemConfiguration_LoadParsesXml()
        {
            var app = LoadAppAssembly();
            var configType = app.GetType("IndustrialProccesingSystemAPI.SystemConfiguration");
            var jobType = app.GetType("IndustrialProccesingSystemAPI.Job");

            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");

            try
            {
                File.WriteAllText(tempFile, string.Join(Environment.NewLine, new[]
                {
                    "<SystemConfig>",
                    "  <WorkerCount>5</WorkerCount>",
                    "  <MaxQueueSize>100</MaxQueueSize>",
                    "  <Jobs>",
                    "    <Job Type=\"Prime\" Payload=\"numbers:10_000,threads:3\" Priority=\"1\" />",
                    "    <Job Type=\"IO\" Payload=\"delay:1_000\" Priority=\"3\" />",
                    "  </Jobs>",
                    "</SystemConfig>"
                }));

                var configuration = configType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { tempFile });

                Assert.AreEqual(5, (int)configType.GetProperty("WorkerCount").GetValue(configuration));
                Assert.AreEqual(100, (int)configType.GetProperty("MaxQueueSize").GetValue(configuration));

                var jobs = ((IEnumerable<object>)configType.GetProperty("InitialJobs").GetValue(configuration)).ToList();
                Assert.AreEqual(2, jobs.Count);
                Assert.AreEqual(Enum.Parse(app.GetType("IndustrialProccesingSystemAPI.JobType"), "Prime"), jobType.GetProperty("Type").GetValue(jobs[0]));
                Assert.AreEqual("numbers:10_000,threads:3", (string)jobType.GetProperty("Payload").GetValue(jobs[0]));
                Assert.AreEqual(1, (int)jobType.GetProperty("Priority").GetValue(jobs[0]));
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
        public void SystemConfiguration_InvalidRootThrows()
        {
            var app = LoadAppAssembly();
            var configType = app.GetType("IndustrialProccesingSystemAPI.SystemConfiguration");

            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");

            try
            {
                File.WriteAllText(tempFile, "<BadRoot></BadRoot>");

                var exception = Assert.ThrowsException<TargetInvocationException>(() =>
                    configType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { tempFile }));

                Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidDataException));
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
        public void JobProcessor_ProcessAsync_PrimeCountsPrimes()
        {
            var app = LoadAppAssembly();
            var processorType = app.GetType("IndustrialProccesingSystemAPI.JobProcessor");
            var jobType = app.GetType("IndustrialProccesingSystemAPI.Job");
            var jobEnum = app.GetType("IndustrialProccesingSystemAPI.JobType");

            var processor = Activator.CreateInstance(processorType);
            var job = CreateJob(jobType, jobEnum, "Prime", "numbers:10_000,threads:3", 1);

            var result = AwaitTaskResult(processorType.GetMethod("ProcessAsync").Invoke(processor, new[] { job }));

            Assert.AreEqual(1229, result);
        }

        [TestMethod]
        public void JobProcessor_ProcessAsync_InvalidPrimePayloadThrows()
        {
            var app = LoadAppAssembly();
            var processorType = app.GetType("IndustrialProccesingSystemAPI.JobProcessor");
            var jobType = app.GetType("IndustrialProccesingSystemAPI.Job");
            var jobEnum = app.GetType("IndustrialProccesingSystemAPI.JobType");

            var processor = Activator.CreateInstance(processorType);
            var job = CreateJob(jobType, jobEnum, "Prime", "numbers:bad", 1);
            var task = processorType.GetMethod("ProcessAsync").Invoke(processor, new[] { job });

            var exception = Assert.ThrowsException<TargetInvocationException>(() => AwaitTaskResult(task));
            Assert.IsInstanceOfType(exception.InnerException, typeof(FormatException));
        }

        [TestMethod]
        public void JobProcessor_ProcessAsync_IoPayloadAcceptsDelayFormat()
        {
            var app = LoadAppAssembly();
            var processorType = app.GetType("IndustrialProccesingSystemAPI.JobProcessor");
            var jobType = app.GetType("IndustrialProccesingSystemAPI.Job");
            var jobEnum = app.GetType("IndustrialProccesingSystemAPI.JobType");

            var processor = Activator.CreateInstance(processorType);
            var job = CreateJob(jobType, jobEnum, "IO", "delay:1_000", 1);

            var result = AwaitTaskResult(processorType.GetMethod("ProcessAsync").Invoke(processor, new[] { job }));

            Assert.IsTrue(result >= 0 && result <= 100);
        }

        [TestMethod]
        public void JobReportService_GenerateReportCreatesXmlFile()
        {
            var app = LoadAppAssembly();
            var reportServiceType = app.GetType("IndustrialProccesingSystemAPI.JobReportService");
            var snapshotType = app.GetType("IndustrialProccesingSystemAPI.JobExecutionSnapshot");
            var jobEnum = app.GetType("IndustrialProccesingSystemAPI.JobType");

            var reportDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                var service = Activator.CreateInstance(reportServiceType, reportDirectory);
                var snapshotListType = typeof(List<>).MakeGenericType(snapshotType);
                var snapshots = (System.Collections.IList)Activator.CreateInstance(snapshotListType);
                snapshots.Add(Activator.CreateInstance(snapshotType, Enum.Parse(jobEnum, "Prime"), 2, 1, new List<int> { 10, 20 }));
                snapshots.Add(Activator.CreateInstance(snapshotType, Enum.Parse(jobEnum, "IO"), 1, 0, new List<int> { 30 }));

                reportServiceType.GetMethod("GenerateReport").Invoke(service, new object[] { snapshots });

                var reportPath = Path.Combine(reportDirectory, "report_01.xml");
                Assert.IsTrue(File.Exists(reportPath));

                var reportText = File.ReadAllText(reportPath);
                Assert.IsTrue(reportText.Contains("<Report>"));
            }
            finally
            {
                if (Directory.Exists(reportDirectory))
                {
                    Directory.Delete(reportDirectory, true);
                }
            }
        }

        [TestMethod]
        public void ProcessingSystem_SubmitRejectsNullJob()
        {
            var app = LoadAppAssembly();
            var systemType = app.GetType("IndustrialProccesingSystemAPI.ProcessingSystem");

            var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var logPath = Path.Combine(baseDirectory, "events.txt");
            var reportDirectory = Path.Combine(baseDirectory, "reports");

            try
            {
                Directory.CreateDirectory(baseDirectory);
                var system = (IDisposable)Activator.CreateInstance(systemType, 1, 10, null, logPath, reportDirectory);

                var exception = Assert.ThrowsException<TargetInvocationException>(() =>
                    systemType.GetMethod("Submit").Invoke(system, new object[] { null }));
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));

                system.Dispose();
            }
            finally
            {
                if (Directory.Exists(baseDirectory))
                {
                    Directory.Delete(baseDirectory, true);
                }
            }
        }

        [TestMethod]
        public void JobEventLogger_WritesCompletedFailedAndAbortedEntries()
        {
            var app = LoadAppAssembly();
            var loggerType = app.GetType("IndustrialProccesingSystemAPI.JobEventLogger");
            var jobType = app.GetType("IndustrialProccesingSystemAPI.Job");
            var jobEnum = app.GetType("IndustrialProccesingSystemAPI.JobType");

            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var logPath = Path.Combine(directory, "events.txt");

            try
            {
                Directory.CreateDirectory(directory);
                var logger = Activator.CreateInstance(loggerType, logPath);
                var job = CreateJob(jobType, jobEnum, "IO", "payload", 1);

                loggerType.GetMethod("WriteCompletedAsync").Invoke(logger, new object[] { job, 42 });
                loggerType.GetMethod("WriteFailedAsync").Invoke(logger, new object[] { job, new InvalidOperationException("boom") });
                loggerType.GetMethod("WriteAbortedAsync").Invoke(logger, new object[] { job });

                var lines = File.ReadAllLines(logPath);
                Assert.AreEqual(3, lines.Length);
                Assert.IsTrue(lines[0].Contains("[COMPLETED]"));
                Assert.IsTrue(lines[1].Contains("[FAILED]"));
                Assert.IsTrue(lines[2].Contains("[ABORT]"));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static Assembly LoadAppAssembly()
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\IndustrialProccesingSystemAPI\bin\Debug\IndustrialProccesingSystemAPI.exe")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\IndustrialProccesingSystemAPI\bin\Debug\IndustrialProccesingSystemAPI.exe")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"IndustrialProccesingSystemAPI.exe")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"IndustrialProccesingSystemAPI.dll"))
            };

            var existing = candidates.FirstOrDefault(File.Exists);
            if (existing == null)
            {
                throw new FileNotFoundException("Could not find IndustrialProccesingSystemAPI assembly.");
            }

            return Assembly.LoadFrom(existing);
        }

        private static object CreateJob(Type jobType, Type jobEnum, string typeName, string payload, int priority, Guid? id = null)
        {
            return Activator.CreateInstance(jobType, id ?? Guid.NewGuid(), Enum.Parse(jobEnum, typeName), payload, priority);
        }

        private static int AwaitTaskResult(object task)
        {
            var awaiter = task.GetType().GetMethod("GetAwaiter").Invoke(task, null);
            return (int)awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null);
        }
    }
}
