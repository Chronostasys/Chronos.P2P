using Chronos.P2P.Client;
using Chronos.P2P.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Chronos.P2P.Test
{
    public class ClientHandler
    {
        [Handler((int)CallMethods.P2PDataTransfer)]
        public void OnReceiveData(UdpContext udpContext)
        {
            IntegrationTest.data = udpContext.Dto.GetData<string>();
        }
    }
    public class IntegrationTest
    {
        internal static string data;
        bool connected = false;
        [Fact]
        public async Task TestIntegration()
        {
            var peer1 = new Peer(8888, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000));
            var peer2 = new Peer(8800, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000));
            var server = new P2PServer();
            server.AddDefaultServerHandler();

            peer1.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            peer2.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            peer1.PeerConnected += Peer1_PeerConnected;

            peer1.AddHandlers<ClientHandler>();
            peer2.AddHandlers<ClientHandler>();


            var t1 = peer1.StartPeer();
            var t2 = peer2.StartPeer();
            var t3 = server.StartServerAsync();
            await Task.WhenAny(
                t1,
                t2,
                t3, Task.Delay(10000));
            Assert.True(connected);
            Assert.Null(data);
            var greetingString = "Hi";
            var hello = new Hello { HelloString = greetingString };
            await peer1.SendDataToPeerAsync(greetingString);
            await Task.Delay(1000);
            Assert.Equal(hello.HelloString, data);
            peer1.Dispose();
            peer2.Dispose();
            server.Dispose();
            
        }

        private void Peer1_PeerConnected(object sender, EventArgs e)
        {
            connected = true;
        }

        private void Peer1_PeersDataReceiveed(object sender, EventArgs e)
        {
            var p = sender as Peer;
            if (p.peers.Count!=0)
            {
                p.SetPeer(p.peers.Keys.First());
            }

        }
    }
}
