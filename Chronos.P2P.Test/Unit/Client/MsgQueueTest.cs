using Chronos.P2P.Client;
using System.Threading.Tasks;
using Xunit;

namespace Chronos.P2P.Test
{
    public class MsgQueueTest
    {
        
        [Fact]
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
        [Fact]
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
        [Fact]
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
            await Task.Delay(1);
            Assert.Equal(110, num);
        }


    }
}