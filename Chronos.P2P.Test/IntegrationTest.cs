using Chronos.P2P.Client;
using Chronos.P2P.Server;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
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
                Interlocked.Increment(ref IntegrationTest.nums);
            }
        }
    }

    public class IntegrationTest
    {
        private TaskCompletionSource completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentDictionary<Guid, TaskCompletionSource> sources = new();
        internal static string data;
        internal static int nums;
        Peer peer1;
        Peer peer2;

        private void Peer_PeerConnected(object sender, EventArgs e)
        {
            completionSource.TrySetResult();
        }

        private void Peer_PeersDataReceived(object sender, EventArgs e)
        {
            var p = sender as Peer;
            sources[p.ID].TrySetResult();
        }
        [Fact(DisplayName = "Set Up Test", Timeout = 10000)]
        private async Task SetUpPeers()
        {
            if (peer1 == null)
            {
                peer1 = new Peer(9009, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
                peer2 = new Peer(9020, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            }
            sources[peer1.ID] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            sources[peer2.ID] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            peer1.PeersDataReceived += Peer_PeersDataReceived;
            peer2.PeersDataReceived += Peer_PeersDataReceived;
            peer1.PeerConnected += Peer_PeerConnected;
            peer2.PeerConnected += Peer_PeerConnected;

            peer1.AddHandlers<ClientHandler>();
            peer2.AddHandlers<ClientHandler>();
            Console.WriteLine("start peer 1");
            _ = peer1.StartPeer();
            Console.WriteLine("start peer 2");
            _ = peer2.StartPeer();
            Console.WriteLine("all peers started");
            await sources[peer1.ID].Task;
            await sources[peer2.ID].Task;
            Console.WriteLine("set peer1");
            while (true)
            {
                if (peer1.Peers is not null && peer1.Peers.ContainsKey(peer2.ID))
                {
                    peer1.SetPeer(peer2.ID);
                }
                if (peer2.Peers is not null && peer2.Peers.ContainsKey(peer1.ID))
                {
                    peer2.SetPeer(peer1.ID);
                }
                if (peer1.RmotePeer is not null && peer2.RmotePeer is not null)
                {
                    break;
                }
                await Task.Delay(100);
            }
            Console.WriteLine("set peer2");
            Console.WriteLine("All peers set");
            while (!(peer1.IsPeerConnected && peer2.IsPeerConnected))
            {
                await Task.Delay(100);
            }
            Console.WriteLine("all peers connected");
        }

        [Fact(DisplayName = "File Transfer test", Timeout = 20000)]
        public async Task TestFileTransfer()
        {
            var src = "Tommee Profitt,Jung Youth,Fleurie - In the End.mp3";
            var dst = "transfered.mp3";
            peer1 = new Peer(10999, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5003));
            peer2 = new Peer(30901, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5003));
            var server = new P2PServer(5003);
            server.AddDefaultServerHandler();
            _ = server.StartServerAsync();
            peer1.OnInitFileTransfer = info =>
            {
                return Task.FromResult((true, dst));
            };
            peer2.OnInitFileTransfer = info =>
            {
                return Task.FromResult((true, dst));
            };
            await SetUpPeers();
            await Task.Delay(1000);
            await peer1.SendFileAsync(src);
            await Task.Delay(5000);
            using (var md5 = MD5.Create())
            using (var fs1 = File.OpenRead(src))
            using (var fs2 = File.OpenRead(dst))
            {
                var hash1 = await md5.ComputeHashAsync(fs1);
                var hash2 = await md5.ComputeHashAsync(fs2);
                Assert.True(hash1.SequenceEqual(hash2));
            }
        }

        [Fact(DisplayName = "Local Server Integration test", Timeout = 20000)]
        public async Task TestIntegration()
        {
            nums = 0;
            data = null;
            peer1 = new Peer(9888, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            peer2 = new Peer(9800, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            var server = new P2PServer(5001);
            server.AddDefaultServerHandler();
            _ = server.StartServerAsync();

            await SetUpPeers();
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
                while (true)
                {
                    if (await peer1.SendDataToPeerReliableAsync("test"))
                    {
                        break;
                    }
                }
            }
            await Task.Delay(1000);
            Assert.Equal(1000, nums);
            peer1.Dispose();
            peer2.Dispose();

            server.Dispose();
        }

        [Fact(DisplayName = "Remote Server Integration test", Timeout = 20000)]
        public async Task TestRemoteIntegration()
        {
            data = null;
            peer1 = new Peer(9999, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            peer2 = new Peer(9901, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));

            await SetUpPeers();
            Assert.Null(data);
            var greetingString = "Hi";
            var hello = new Hello { HelloString = greetingString };
            await peer1.SendDataToPeerReliableAsync(greetingString);
            await Task.Delay(1000);
            Assert.Equal(hello.HelloString, data);
            peer1.Dispose();
            peer2.Dispose();
        }
    }
}
