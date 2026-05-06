using System;

namespace IndustrialProccesingSystemAPI
{
    public class Job
    {
        public Guid Id { get; }
        public JobType Type { get; }
        public string Payload { get; }
        public int Priority { get; }

        public Job(Guid id, JobType type, string payload, int priority)
        {
            Id = id;
            Type = type;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Priority = priority;
        }
    }
}
