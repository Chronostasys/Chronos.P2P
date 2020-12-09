using Chronos.P2P.Server;

namespace Chronos.P2P.Test
{
    public class DITestHandler
    {
        internal string diString;
        internal Hello hello;
        internal P2PServer p2PServer;

        public DITestHandler(P2PServer server, string distring)
        {
            p2PServer = server;
            diString = distring;
        }

        [Handler(1)]
        public void TestHandler(UdpContext udpContext)
        {
            hello = udpContext.GetData<Hello>().Data;
            if (hello.HelloString == "re")
            {
                UdpServerTest.ackNums++;
            }
        }
    }
}