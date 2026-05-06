using System;
using System.Collections.Generic;
using System.Linq;

namespace IndustrialProccesingSystemAPI
{
    public class JobQueue
    {
        private readonly object syncRoot = new object();
        private readonly List<Job> items = new List<Job>();
        private readonly int maxSize;

        public JobQueue(int maxSize)
        {
            if (maxSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            }

            this.maxSize = maxSize;
        }

        public bool TryEnqueue(Job job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            lock (syncRoot)
            {
                if (items.Count >= maxSize)
                {
                    return false;
                }

                items.Add(job);
                return true;
            }
        }

        public bool TryDequeue(out Job job)
        {
            lock (syncRoot)
            {
                if (items.Count == 0)
                {
                    job = null;
                    return false;
                }

                var index = 0;
                for (var i = 1; i < items.Count; i++)
                {
                    if (items[i].Priority < items[index].Priority)
                    {
                        index = i;
                    }
                }

                job = items[index];
                items.RemoveAt(index);
                return true;
            }
        }

        public IReadOnlyList<Job> GetTopJobs(int n)
        {
            lock (syncRoot)
            {
                return items
                    .OrderBy(job => job.Priority)
                    .ThenBy(job => job.Id)
                    .Take(n)
                    .ToList();
            }
        }
    }
}
