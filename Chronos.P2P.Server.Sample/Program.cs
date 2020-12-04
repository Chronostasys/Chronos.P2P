using Chronos.P2P.Client;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Chronos.P2P.Server.Sample
{
    public class ClientHandler
    {
        [Handler((int)CallMethods.P2PDataTransfer)]
        public void OnReceiveData(UdpContext udpContext)
        {
            var d = udpContext.GetData<string>().Data;
            Console.WriteLine(d);
        }
    }
    class Program
    {
        static async Task Main(string[] args)
        {
            //var peer = new Peer(8899, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000));
            //var peer1 = new Peer(8890, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000));
            //var t1 = peer.StartPeer();
            //var t2 = peer1.StartPeer();
            var peer = new Peer(26900, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            var peer1 = new Peer(8890, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            peer.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            peer1.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            peer1.PeerConnected += Peer1_PeerConnected;
            peer.PeerConnected += Peer1_PeerConnected;
            peer.AddHandlers<ClientHandler>();
            peer1.AddHandlers<ClientHandler>();
            peer.StartPeer();
            peer1.StartPeer();
            peer.PeerConnectionLost += Peer_PeerConnectionLost;
            Task.Run(async () =>
            {
                await Task.Delay(10000);
                peer1.Cancel();
            });
            //var server = new P2PServer();
            //server.AddDefaultServerHandler();
            //await server.StartServerAsync();
            while (true)
            {
                await peer.SendDataToPeerAsync(Console.ReadLine());
            }
            
        }

        private static void Peer_PeerConnectionLost(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void Peer1_PeerConnected(object sender, EventArgs e)
        {
            var p = sender as Peer;
            p.SendDataToPeerAsync("Who are you?");
        }

        private static void Peer1_PeersDataReceiveed(object sender, EventArgs e)
        {
            var p = sender as Peer;
            if (p.peers.Count != 0)
            {
                p.SetPeer(p.peers.Keys.First());
            }

        }
    }
}
