using System;
using System.IO;

namespace IndustrialProccesingSystemAPI
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var configurationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemConfiguration.xml");
                var configuration = SystemConfiguration.Load(configurationPath);

                using (var system = new ProcessingSystem(configuration.WorkerCount, configuration.MaxQueueSize, configuration.InitialJobs, configuration.EventLogPath, configuration.ReportDirectory))
                {
                    system.JobCompleted += (job, result) => Console.WriteLine("COMPLETED: {0} {1} => {2}", job.Id, job.Type, result);
                    system.JobFailed += (job, exception) => Console.WriteLine("FAILED: {0} {1} => {2}", job.Id, job.Type, exception.Message);

                    var random = new Random();
                    for (var i = 0; i < configuration.WorkerCount; i++)
                    {
                        var type = random.Next(0, 2) == 0 ? JobType.Prime : JobType.IO;
                        var payload = type == JobType.Prime ? random.Next(500, 5000).ToString() : random.Next(200, 1500).ToString();
                        var job = new Job(Guid.NewGuid(), type, payload, random.Next(1, 5));
                        system.Submit(job);
                    }

                    Console.WriteLine("Press Enter to exit...");
                    Console.ReadLine();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }
    }
}
