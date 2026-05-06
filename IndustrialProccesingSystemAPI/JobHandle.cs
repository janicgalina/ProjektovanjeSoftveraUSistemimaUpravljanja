using System;
using System.Threading.Tasks;

namespace IndustrialProccesingSystemAPI
{
    public class JobHandle
    {
        public Guid Id { get; }
        public Task<int> Result { get; }
        private readonly TaskCompletionSource<int> completionSource;

        internal JobHandle(Guid id, TaskCompletionSource<int> completionSource)
        {
            Id = id;
            completionSource = completionSource ?? throw new ArgumentNullException(nameof(completionSource));
            Result = completionSource.Task;
        }
    }
}
