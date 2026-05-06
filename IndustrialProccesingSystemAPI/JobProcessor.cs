using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IndustrialProccesingSystemAPI
{
    public class JobProcessor
    {
        private readonly Random random = new Random();

        public Task<int> ProcessAsync(Job job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (job.Type == JobType.Prime)
            {
                return Task.Run(() => CountPrimes(job.Payload));
            }

            return Task.Run(() => ReadIoValue(job.Payload));
        }

        private int CountPrimes(string payload)
        {
            int limit;
            if (!TryParseNumberPayload(payload, out limit))
            {
                throw new FormatException("Prime payload must be an integer.");
            }

            if (limit < 2)
            {
                return 0;
            }

            var partitionCount = Math.Min(8, Math.Max(1, limit));
            var partitionSize = Math.Max(1, (int)Math.Ceiling(limit / (double)partitionCount));
            var ranges = new List<Tuple<int, int>>();
            for (var start = 2; start <= limit; start += partitionSize)
            {
                var end = Math.Min(limit, start + partitionSize - 1);
                ranges.Add(Tuple.Create(start, end));
            }

            var results = new int[ranges.Count];
            Parallel.ForEach(Enumerable.Range(0, ranges.Count), new ParallelOptions { MaxDegreeOfParallelism = partitionCount }, index =>
            {
                var range = ranges[index];
                var count = 0;
                for (var value = range.Item1; value <= range.Item2; value++)
                {
                    if (IsPrime(value))
                    {
                        count++;
                    }
                }

                results[index] = count;
            });

            return results.Sum();
        }

        private int ReadIoValue(string payload)
        {
            var delayMs = 200 + SafeRandom(0, 250);
            int parsedDelay;
            if (TryParseDelayPayload(payload, out parsedDelay))
            {
                delayMs = parsedDelay;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(delayMs));
            return SafeRandom(0, 101);
        }

        private bool TryParseNumberPayload(string payload, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            var text = payload;
            if (text.StartsWith("numbers:", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring("numbers:".Length);
                var commaIndex = text.IndexOf(',');
                if (commaIndex >= 0)
                {
                    text = text.Substring(0, commaIndex);
                }
            }

            text = text.Replace("_", string.Empty);
            return int.TryParse(text, out value);
        }

        private bool TryParseDelayPayload(string payload, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            var text = payload;
            if (text.StartsWith("delay:", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring("delay:".Length);
            }

            text = text.Replace("_", string.Empty);
            return int.TryParse(text, out value);
        }

        private int SafeRandom(int minValue, int maxValue)
        {
            lock (random)
            {
                return random.Next(minValue, maxValue);
            }
        }

        private bool IsPrime(int value)
        {
            if (value < 2)
            {
                return false;
            }

            if (value == 2)
            {
                return true;
            }

            if (value % 2 == 0)
            {
                return false;
            }

            for (var divisor = 3; divisor * divisor <= value; divisor += 2)
            {
                if (value % divisor == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
