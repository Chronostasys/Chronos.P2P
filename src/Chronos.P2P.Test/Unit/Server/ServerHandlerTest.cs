using System;
using System.Threading.Tasks;
using System.Reflection.Metadata;
using Chronos.P2P.Server;
using Xunit;
using Moq;
using Chronos.P2P.Client;
using System.Text.Json;

namespace Chronos.P2P.Test.Unit.Server
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
            var bytes = JsonSerializer.SerializeToUtf8Bytes<CallServerDto<ConnectionReplyDto>>(new CallServerDto<ConnectionReplyDto>
            {
                Data = new ConnectionReplyDto
                {
                    Acc = true,
                    Ep = PeerEP.ParsePeerEPFromIPEP(new System.Net.IPEndPoint(100,100))
                },
            });
            handler.HolePunchRequest(new UdpContext(bytes, new(), 
                new System.Net.IPEndPoint(100,100), null));
            Assert.Equal(0,msgs.Count);
        }
        [Fact(Timeout = 2000)]
        public void TestLegalConnectionHandshake()
        {
            var ep = new System.Net.IPEndPoint(100, 1000);
            var pep = PeerEP.ParsePeerEPFromIPEP(ep);
            var moq = new Mock<P2PServer>(() => new P2PServer(50500));
            moq.Setup(s => s.StartSendTask()).Returns(Task.CompletedTask);
            var obj = moq.Object;
            obj.connectionDic[pep] = (pep, DateTime.UtcNow);
            var msgs = obj.msgs;
            var handler = new ServerHandlers(obj);
            var bytes = JsonSerializer.SerializeToUtf8Bytes<CallServerDto<ConnectionReplyDto>>(new CallServerDto<ConnectionReplyDto>
            {
                Data = new ConnectionReplyDto
                {
                    Acc = true,
                    Ep = pep
                },
            });
            handler.HolePunchRequest(new UdpContext(bytes, new(),
                ep, null));
            Assert.Equal(3, msgs.Count);
        }
    }
}