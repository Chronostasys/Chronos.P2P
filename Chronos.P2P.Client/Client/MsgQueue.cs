using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public class MsgQueue<T>
    {
        private ConcurrentQueue<T> queue = new();
        private SemaphoreSlim semaphore = new(0);

        public async Task<T> DequeueAsync()
        {
            await semaphore.WaitAsync();
            while (true)
            {
                if (queue.TryDequeue(out var result))
                {
                    return result;
                }
            }
        }

        public void Enqueue(T result)
        {
            semaphore.Release();
            queue.Enqueue(result);
        }

        public Task WaitDataAsync()
        {
            return semaphore.WaitAsync();
        }
    }
}