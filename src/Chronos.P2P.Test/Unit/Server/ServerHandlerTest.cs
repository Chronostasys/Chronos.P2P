using System;
using System.Threading.Tasks;
using System.Reflection.Metadata;
using Chronos.P2P.Server;
using Xunit;
using Moq;
using Chronos.P2P.Client;


namespace Chronos.P2P.Test
{
    public class ServerHandlerTest
    {
        [Fact(Timeout = 2000)]
        public void TestIllegalConnectionHandshake()
        {
            
            var moq = new Mock<P2PServer>(()=>new P2PServer(5500));
            moq.Setup(s => s.StartSendTask()).Returns(Task.CompletedTask);
            var obj = moq.Object;
            var msgs = obj.msgs;
            var handler = new ServerHandlers(obj);
            var bytes = P2PServer.CreateUdpRequestBuffer(0, Guid.Empty, new ConnectionReplyDto
            {
                Acc = true,
                Ep = PeerEP.ParsePeerEPFromIPEP(new System.Net.IPEndPoint(100, 100))
            });

            handler.HolePunchRequest(new UdpContext(bytes.AsMemory()[20..].ToArray(), new(), 
                new System.Net.IPEndPoint(100,100), null));
            Assert.Equal(0,msgs.Count);
        }
        [Fact(Timeout = 2000)]
        public void TestLegalConnectionHandshake()
        {
            var ep = new System.Net.IPEndPoint(100, 1000);
            var pep = PeerEP.ParsePeerEPFromIPEP(ep);
            var moq = new Mock<P2PServer>(() => new P2PServer(10500));
            moq.Setup(s => s.StartSendTask()).Returns(Task.CompletedTask);
            var obj = moq.Object;
            obj.connectionDic[pep] = (pep, DateTime.UtcNow);
            var msgs = obj.msgs;
            var handler = new ServerHandlers(obj);
            var bytes = P2PServer.CreateUdpRequestBuffer(0, Guid.Empty, new ConnectionReplyDto
            {
                Acc = true,
                Ep = pep
            });
            handler.HolePunchRequest(new UdpContext(bytes.AsMemory()[20..].ToArray(), new(),
                ep, null));
            Assert.Equal(3, msgs.Count);
        }
    }
}