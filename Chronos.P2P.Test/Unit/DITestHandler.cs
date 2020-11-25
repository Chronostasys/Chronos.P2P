using Chronos.P2P.Server;
using System;

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
            hello = udpContext.GetData<Hello>().Data;
            if (hello.HelloString != "hello")
            {
                throw new Exception();
            }
        }
    }
}
