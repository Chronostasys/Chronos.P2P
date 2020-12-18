using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public class MsgQueue<T>: IAsyncEnumerable<T>
    {
        private ConcurrentQueue<T> queue = new();
        private SemaphoreSlim semaphore = new(0);

        public int Count => queue.Count;

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

        /// <summary>
        /// the enumerator returned bu this type is endless, unless you cancel the enumeration
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return await DequeueAsync();
            }
        }
    }
}