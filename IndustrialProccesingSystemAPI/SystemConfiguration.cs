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

            if (!string.Equals(root.Name.LocalName, "SystemConfig", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Invalid configuration root element.");
            }

            var workerCount = ReadInt(root, "WorkerCount", 1);
            var maxQueueSize = ReadInt(root, "MaxQueueSize", 100);
            var eventLogPath = ReadString(root, "EventLogPath", "event-log.txt");
            var reportDirectory = ReadString(root, "ReportDirectory", "Reports");

            var initialJobs = new List<Job>();
            var jobsElement = root.Element("Jobs");
            if (jobsElement != null)
            {
                foreach (var jobElement in jobsElement.Elements("Job"))
                {
                    initialJobs.Add(new Job(
                        Guid.NewGuid(),
                        ParseJobType(ReadAttribute(jobElement, "Type", "Prime")),
                        ReadAttribute(jobElement, "Payload", string.Empty),
                        ReadAttributeInt(jobElement, "Priority", 1)));
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

        private static string ReadAttribute(XElement element, string attributeName, string defaultValue)
        {
            var attribute = element.Attribute(attributeName);
            return attribute == null ? defaultValue : attribute.Value;
        }

        private static int ReadAttributeInt(XElement element, string attributeName, int defaultValue)
        {
            int value;
            return int.TryParse(ReadAttribute(element, attributeName, null), out value) ? value : defaultValue;
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
