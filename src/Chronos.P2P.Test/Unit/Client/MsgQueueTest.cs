using Chronos.P2P.Client;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Chronos.P2P.Test
{
    public class MsgQueueTest
    {
        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(976)]
        [InlineData(9888)]
        public void TestCount(int num)
        {
            var queue = new MsgQueue<int>();
            for (int i = 0; i < num; i++)
            {
                queue.Enqueue(i);
            }
            Assert.Equal(num, queue.Count);
        }

        [Fact(Timeout = 2000)]
        public async Task TestIAsyncEnumerableApi()
        {
            int num = 0;
            var queue = new MsgQueue<int>();
            _ = Task.Run(() =>
            {
                queue.Enqueue(50);
                queue.Enqueue(60);
                queue.Enqueue(70);
            });
            await Task.WhenAny(Task.Run(async () =>
            {
                await foreach (var item in queue)
                {
                    Assert.Equal(10 * num + 50, item);
                    num++;
                }
            }), Task.Delay(100));
            Assert.Equal(3, num);
        }

        [Fact(Timeout = 2000)]
        public async Task TestIAsyncEnumerableCancel()
        {
            var queue = new MsgQueue<int>();
            var tokenSrc = new CancellationTokenSource();
            _ = Task.Run(() =>
            {
                tokenSrc.CancelAfter(100);
            });
            try
            {
                await foreach (var item in queue.WithCancellation(tokenSrc.Token))
                {
                }
            }
            catch (System.Exception e)
            {
                Assert.IsType<OperationCanceledException>(e);
            }
        }

        [Fact(Timeout = 2000)]
        public async Task TestMsgQueueDequeue()
        {
            var queue = new MsgQueue<int>();
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                queue.Enqueue(50);
            });
            var val = await queue.DequeueAsync();
            Assert.Equal(50, val);
        }

        [Fact(Timeout = 2000)]
        public async Task TestMsgQueueWait()
        {
            int n = 0;
            var queue = new MsgQueue<int>();
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                n = 100;
                queue.Enqueue(50);
            });
            var val = await queue.DequeueAsync();
            Assert.Equal(100, n);
        }

        [Fact(Timeout = 2000)]
        public async Task TestStartQueueTask()
        {
            int num = 0;
            var queue = new MsgQueue<int>();
            _ = Utils.StartQueuedTask(queue, i =>
            {
                num += i;
                return Task.CompletedTask;
            });
            queue.Enqueue(50);
            queue.Enqueue(60);
            await Task.Delay(100);
            Assert.Equal(110, num);
        }
    }
}