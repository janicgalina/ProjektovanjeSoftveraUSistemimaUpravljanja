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
