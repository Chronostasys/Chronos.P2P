using Chronos.P2P.Server;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace Chronos.P2P.Test
{
    public class DITestHandler
    {
        internal P2PServer p2PServer;
        internal string diString;
        internal Hello hello;
        public DITestHandler(P2PServer server, string distring)
        {
            p2PServer = server;
            diString = distring;
        }
        [Handler(1)]
        public void TestHandler(UdpContext udpContext)
        {
            hello = udpContext.Dto.GetData<Hello>();
            if (hello.HelloString != "hello")
            {
                throw new Exception();
            }
        }
    }
    public class Hello
    {
        public string HelloString { get; set; }
    }
    public class UdpServerTest
    {
        P2PServer server;
        string s = "test string";
        public UdpServerTest()
        {
        }
        private void SetUp()
        {
            server = new P2PServer();
            server.AddHandler<DITestHandler>();

            server.ConfigureServices(services =>
            {
                services.AddSingleton(s);
            });
        }
        [Fact]
        public void TestCtorDI()
        {
            SetUp();
            var handler = server.GetInstance(server.requestHandlers[1]) as DITestHandler;
            Assert.NotNull(handler);
            Assert.Equal(server, handler.p2PServer);
            Assert.Equal(s, handler.diString);
            server.Dispose();
        }
        [Fact]
        public void TestHandlerAttribute()
        {
            SetUp();
            var hello = new Hello { HelloString = "hello" };
            server.CallHandler(server.requestHandlers[1], new UdpContext
            {
                
                Dto = new Client.CallServerDto<object>
                {
                    Method = 1,
                    Data = hello
                }
            });
            server.Dispose();
        }
    }
}
