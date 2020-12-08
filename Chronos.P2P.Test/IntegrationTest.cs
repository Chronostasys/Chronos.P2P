#define TEST
using Chronos.P2P.Client;
using Chronos.P2P.Server;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Chronos.P2P.Test
{
    public class ClientHandler
    {
        [Handler((int)CallMethods.P2PDataTransfer)]
        public void OnReceiveData(UdpContext udpContext)
        {
            IntegrationTest.data = udpContext.GetData<string>().Data;
            if (IntegrationTest.data is "test")
            {
                IntegrationTest.nums++;
            }
        }
    }

    public class IntegrationTest
    {
        internal static string data;
        internal static int nums;
        TaskCompletionSource completionSource = new();
        TaskCompletionSource completionSource1 = new();
        TaskCompletionSource getPeerCompletionSource = new();
        TaskCompletionSource getPeerCompletionSource1 = new();

        private void Peer1_PeerConnected(object sender, EventArgs e)
        {
            Console.WriteLine("a peer connected");
            Task.Run(() =>
            {
                completionSource.SetResult();
            });

        }
        private void Peer_PeerConnected(object sender, EventArgs e)
        {
            Console.WriteLine("a peer connected");
            Task.Run(() =>
            {
                completionSource1.SetResult();
            });

        }

        private void Peer1_PeersDataReceiveed(object sender, EventArgs e)
        {
            Console.WriteLine("Peer1_PeersDataReceiveed called");
            var p = sender as Peer;
            if (!p.peers.IsEmpty)
            {
                getPeerCompletionSource.TrySetResult();
                //p.SetPeer(p.peers.Keys.First(), true);
            }
        }
        private void Peer_PeersDataReceiveed(object sender, EventArgs e)
        {
            Console.WriteLine("Peer1_PeersDataReceiveed called");
            var p = sender as Peer;
            if (!p.peers.IsEmpty)
            {
                getPeerCompletionSource1.TrySetResult();
                //p.SetPeer(p.peers.Keys.First(), true);
            }
        }

        [Fact(DisplayName = "Local Server Integration test", Timeout = 10000)]
        public async Task TestIntegration()
        {
            Console.WriteLine("LocalTest");
            nums = 0;
            data = null;
            var peer1 = new Peer(8888, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            var peer2 = new Peer(8800, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            var server = new P2PServer(5001);
            server.AddDefaultServerHandler();

            peer1.PeersDataReceiveed += Peer_PeersDataReceiveed;
            peer2.PeersDataReceiveed += Peer_PeersDataReceiveed;
            peer1.PeerConnected += Peer_PeerConnected;
            peer2.PeerConnected += Peer_PeerConnected;

            peer1.AddHandlers<ClientHandler>();
            peer2.AddHandlers<ClientHandler>();

            _ = server.StartServerAsync();
            _ = peer1.StartPeer();
            _ = peer2.StartPeer();
            Console.WriteLine("waiting for peer data");
            
            await getPeerCompletionSource1.Task;
            Console.WriteLine("peer data received");
            while (true)
            {
                if (!peer1.peers.IsEmpty && peer1.peers.ContainsKey(peer2.ID))
                {
                    peer1.SetPeer(peer2.ID);
                    Console.WriteLine("peer1 set peer");
                    break;
                }
                await Task.Delay(100);
            }
            while (true)
            {
                if (!peer2.peers.IsEmpty && peer2.peers.ContainsKey(peer1.ID))
                {
                    peer2.SetPeer(peer1.ID);
                    Console.WriteLine("peer2 set peer");
                    break;
                }
                await Task.Delay(100);
            }
            await completionSource1.Task;
            Assert.Null(data);
            var greetingString = "Hi";
            var hello = new Hello { HelloString = greetingString };
            await peer1.SendDataToPeerAsync(greetingString);
            await peer1.SendDataToPeerAsync(greetingString);
            await peer1.SendDataToPeerAsync(greetingString);
            await Task.Delay(1000);
            Assert.Equal(hello.HelloString, data);
            for (int i = 0; i < 1000; i++)
            {
                await peer1.SendDataToPeerReliableAsync("test");
            }
            Assert.Equal(1000, nums);
            peer1.Dispose();
            peer2.Dispose();
            server.Dispose();
        }

        [Fact(DisplayName = "Remote Server Integration test", Timeout = 10000)]
        public async Task TestRemoteIntegration()
        {
            Console.WriteLine("RemoteTest");
            data = null;
            var peer1 = new Peer(8999, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            var peer2 = new Peer(8901, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));

            peer1.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            peer2.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            peer1.PeerConnected += Peer1_PeerConnected;
            peer2.PeerConnected += Peer1_PeerConnected;

            peer1.AddHandlers<ClientHandler>();
            peer2.AddHandlers<ClientHandler>();

            _ = peer1.StartPeer();
            _ = peer2.StartPeer();
            Console.WriteLine("waiting for peer data");
            await getPeerCompletionSource.Task;
            Console.WriteLine("peer data received");
            while (true)
            {
                if (!peer1.peers.IsEmpty && peer1.peers.ContainsKey(peer2.ID))
                {
                    peer1.SetPeer(peer2.ID);
                    Console.WriteLine("peer1 set peer");
                    break;
                }
                await Task.Delay(100);
            }
            while (true)
            {
                if (!peer2.peers.IsEmpty && peer2.peers.ContainsKey(peer1.ID))
                {
                    peer2.SetPeer(peer1.ID);
                    Console.WriteLine("peer2 set peer");
                    break;
                }
                await Task.Delay(100);
            }
            await completionSource.Task;
            Assert.Null(data);
            var greetingString = "Hi";
            var hello = new Hello { HelloString = greetingString };
            await peer1.SendDataToPeerAsync(greetingString);
            await peer1.SendDataToPeerAsync(greetingString);
            await peer1.SendDataToPeerAsync(greetingString);
            await Task.Delay(1000);
            Assert.Equal(hello.HelloString, data);
            peer1.Dispose();
            peer2.Dispose();
        }
    }
}