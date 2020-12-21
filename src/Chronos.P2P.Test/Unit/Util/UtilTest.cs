using System.Net;
using System.Threading.Tasks;
using Chronos.P2P.Client;
using Xunit;
using System.Linq;

namespace Chronos.P2P.Test
{
    public class UtilTest
    {
        [Theory]
        [InlineData("192.168.1.211", true)]
        [InlineData("192.168.1.5", true)]
        [InlineData("192.168.2.211", false)]
        [InlineData("192.18.1.211", false)]
        public void TestIsInSubnet(string address, bool actual)
        {
            var isInSubNet = Utils.IsInSameSubnet(IPAddress.Parse("192.168.1.123"),
                IPAddress.Parse(address),IPAddress.Parse("255.255.255.0"));
            Assert.Equal(actual, isInSubNet);
        }
        [Theory]
        [InlineData(100, 200, 300)]
        [InlineData(1, 290, 810)]
        [InlineData(100, 200, 300, 130, 209, 320)]
        public async Task TestStartQueueTask(params int[] nums)
        {
            var msgs = new MsgQueue<int>();
            int num = 0;
            _ = Utils.StartQueuedTask(msgs, (i) =>
             {
                 num += i;
                 return Task.CompletedTask;
             });
            foreach (var item in nums)
            {
                msgs.Enqueue(item);
            }
            await Task.Delay(100);
            Assert.Equal(nums.Sum(), num);


        }
    }
}