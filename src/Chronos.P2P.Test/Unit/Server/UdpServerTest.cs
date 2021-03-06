using Chronos.P2P.Server;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Sockets;

using System.Threading.Tasks;
using Xunit;

namespace Chronos.P2P.Test
{
    public class UdpServerTest
    {
        private const string s = "hello";
        private static Guid id = Guid.NewGuid();

        private readonly byte[] callBytes
            = P2PServer.CreateUdpRequestBuffer(1, id, new Hello { HelloString = "re" });

        private readonly IPEndPoint ep = new(1000, 1000);
        private P2PServer server;
        internal static int ackNums = 0;

        [Fact]
        private void SetUpTest()
        {
            server = new P2PServer(Guid.NewGuid().GetHashCode() % 5000 + 15000);
            server.AddHandler<DITestHandler>();

            server.ConfigureServices(services =>
            {
                services.AddSingleton(s);
            });
        }

        [Fact]
        public void TestCtorDI()
        {
            SetUpTest();
            var handler = server.GetInstance(server.requestHandlers[1]) as DITestHandler;
            Assert.NotNull(handler);
            Assert.Equal(server, handler.p2PServer);
            Assert.Equal(s, handler.diString);
            server.Dispose();
        }

        [Fact]
        public void TestHandlerAttribute()
        {
            SetUpTest();
            var hello = new Hello { HelloString = s };
            server.CallHandler(server.requestHandlers[1], new UdpContext(
                callBytes, new(), new(1000, 1000), new(SocketType.Dgram, ProtocolType.Udp), new ReceiveBufferOwner(callBytes)));
            server.Dispose();
        }

        [Fact]
        public void TestProcessRequestCreateNewThread()
        {
            SetUpTest();
            _ = server.ProcessRequestAsync(null, new SocketReceiveFromResult());
        }

        [Fact]
        public async Task TestReliableRequest()
        {
            SetUpTest();
            ackNums = 0;
            Assert.Equal(0, ackNums);
            Assert.False(server.guidDic.ContainsKey(id));
            var from = new SocketReceiveFromResult
            {
                ReceivedBytes = callBytes.Length,
                RemoteEndPoint = ep
            };
            await server.ProcessRequestAsync(new ReceiveBufferOwner(callBytes), from);
            await Task.Delay(100);
            Assert.Equal(1, ackNums);
            Assert.True(server.guidDic.ContainsKey(id));
            await server.ProcessRequestAsync(new ReceiveBufferOwner(callBytes), from);
            await Task.Delay(100);
            Assert.Equal(1, ackNums);
            Assert.True(server.guidDic.ContainsKey(id));
            server.Dispose();
        }
    }
}