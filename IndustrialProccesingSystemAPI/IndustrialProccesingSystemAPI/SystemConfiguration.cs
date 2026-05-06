using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace IndustrialProccesingSystemAPI
{
    public class SystemConfiguration
    {
        public int WorkerCount { get; }
        public int MaxQueueSize { get; }
        public List<Job> InitialJobs { get; }
        public string EventLogPath { get; }
        public string ReportDirectory { get; }

        public SystemConfiguration(int workerCount, int maxQueueSize, List<Job> initialJobs, string eventLogPath, string reportDirectory)
        {
            WorkerCount = workerCount;
            MaxQueueSize = maxQueueSize;
            InitialJobs = initialJobs ?? new List<Job>();
            EventLogPath = eventLogPath;
            ReportDirectory = reportDirectory;
        }

        public static SystemConfiguration Load(string path)
        {
            var document = XDocument.Load(path);
            var root = document.Root;
            if (root == null)
            {
                throw new InvalidDataException("Configuration file is empty.");
            }

            var workerCount = ReadInt(root, "WorkerCount", 1);
            var maxQueueSize = ReadInt(root, "MaxQueueSize", 100);
            var eventLogPath = ReadString(root, "EventLogPath", "event-log.txt");
            var reportDirectory = ReadString(root, "ReportDirectory", "Reports");

            var initialJobs = new List<Job>();
            var jobsElement = root.Element("InitialJobs");
            if (jobsElement != null)
            {
                foreach (var jobElement in jobsElement.Elements("Job"))
                {
                    initialJobs.Add(new Job(
                        ReadGuid(jobElement, "Id"),
                        ParseJobType(ReadString(jobElement, "Type", "Prime")),
                        ReadString(jobElement, "Payload", string.Empty),
                        ReadInt(jobElement, "Priority", 1)));
                }
            }

            return new SystemConfiguration(workerCount, maxQueueSize, initialJobs, eventLogPath, reportDirectory);
        }

        private static int ReadInt(XElement element, string childName, int defaultValue)
        {
            var child = element.Element(childName);
            if (child == null)
            {
                return defaultValue;
            }

            int value;
            return int.TryParse(child.Value, out value) ? value : defaultValue;
        }

        private static string ReadString(XElement element, string childName, string defaultValue)
        {
            var child = element.Element(childName);
            return child == null ? defaultValue : child.Value;
        }

        private static Guid ReadGuid(XElement element, string childName)
        {
            var child = element.Element(childName);
            Guid value;
            if (child != null && Guid.TryParse(child.Value, out value))
            {
                return value;
            }

            return Guid.NewGuid();
        }

        private static JobType ParseJobType(string value)
        {
            JobType type;
            return Enum.TryParse(value, true, out type) ? type : JobType.Prime;
        }
    }
}
