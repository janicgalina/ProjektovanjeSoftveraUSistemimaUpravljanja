using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace IndustrialProccesingSystemAPI
{
    public class JobReportService
    {
        private readonly string reportDirectory;
        private int nextReportIndex = 1;

        public JobReportService(string reportDirectory)
        {
            this.reportDirectory = reportDirectory ?? throw new ArgumentNullException(nameof(reportDirectory));
            Directory.CreateDirectory(this.reportDirectory);
        }

        public void GenerateReport(IEnumerable<JobExecutionSnapshot> snapshots)
        {
            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            var items = snapshots.ToList();
            var report = new XElement("Report",
                new XElement("GeneratedAt", DateTime.Now),
                new XElement("CompletedByType",
                    items.Where(item => item.CompletedCount > 0)
                        .GroupBy(item => item.Type)
                        .Select(group => new XElement("TypeGroup",
                            new XAttribute("Type", group.Key),
                            new XAttribute("Count", group.Sum(item => item.CompletedCount))))),
                new XElement("AverageExecutionTimeByType",
                    items.Where(item => item.CompletedCount > 0)
                        .GroupBy(item => item.Type)
                        .Select(group => new XElement("TypeGroup",
                            new XAttribute("Type", group.Key),
                            new XAttribute("AverageMs", (int)group.SelectMany(item => item.CompletedDurationsMs).DefaultIfEmpty(0).Average())))),
                new XElement("FailedByType",
                    items.Where(item => item.FailedCount > 0)
                        .GroupBy(item => item.Type)
                        .Select(group => new XElement("TypeGroup",
                            new XAttribute("Type", group.Key),
                            new XAttribute("Count", group.Sum(item => item.FailedCount))))));

            var reportPath = Path.Combine(reportDirectory, string.Format("report_{0:00}.xml", nextReportIndex));
            report.Save(reportPath);
            nextReportIndex++;
            if (nextReportIndex > 10)
            {
                nextReportIndex = 1;
            }
        }
    }

    public class JobExecutionSnapshot
    {
        public JobType Type { get; }
        public int CompletedCount { get; }
        public int FailedCount { get; }
        public IReadOnlyList<int> CompletedDurationsMs { get; }

        public JobExecutionSnapshot(JobType type, int completedCount, int failedCount, IReadOnlyList<int> completedDurationsMs)
        {
            Type = type;
            CompletedCount = completedCount;
            FailedCount = failedCount;
            CompletedDurationsMs = completedDurationsMs ?? new List<int>();
        }
    }
}
