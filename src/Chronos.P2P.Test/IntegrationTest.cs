﻿using Chronos.P2P.Client;
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

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Chronos.P2P.Test
{
    public class ClientHandler
    {
        private readonly Peer peer;

        public ClientHandler(Peer p)
        {
            peer = p;
        }

        [Handler((int)CallMethods.P2PDataTransfer)]
        public void OnReceiveData(UdpContext udpContext)
        {
            IntegrationTest.data[peer.ID] = udpContext.GetData<string>();
            if (IntegrationTest.data[peer.ID] is "test")
            {
                Interlocked.Increment(ref IntegrationTest.nums);
            }
        }
    }

    [Collection("Integration")]
    public class IntegrationTest
    {
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource> completionSource = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource> sources = new();
        private Peer peer1;
        private Peer peer2;
        internal static ConcurrentDictionary<Guid, string> data = new();
        internal static int nums = 0;

        private void Peer_PeerConnected(object sender, EventArgs e)
        {
            var p = sender as Peer;
            completionSource[p.ID].TrySetResult();
        }

        private void Peer_PeersDataReceived(object sender, EventArgs e)
        {
            var p = sender as Peer;
            if (!p.Peers.IsEmpty)
            {
                sources[p.ID].TrySetResult();
            }
        }

        [Fact(Timeout = 20000)]
        private async Task SetUpPeers()
        {
            if (peer1 == null)
            {
                peer1 = new Peer(9009, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 45000));
                peer2 = new Peer(9020, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 45000));
                var server = new P2PServer(45000);
                server.AddDefaultServerHandler();
                _ = server.StartServerAsync();
            }
            completionSource[peer1.ID] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource[peer2.ID] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            sources[peer1.ID] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            sources[peer2.ID] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            peer1.PeersDataReceived += Peer_PeersDataReceived;
            peer2.PeersDataReceived += Peer_PeersDataReceived;
            peer1.PeerConnected += Peer_PeerConnected;
            peer2.PeerConnected += Peer_PeerConnected;

            peer1.AddHandler<ClientHandler>();
            peer2.AddHandler<ClientHandler>();
            Assert.Null(peer1.Peers);
            Assert.Null(peer2.Peers);
            Console.WriteLine("start peer 1");
            _ = peer1.StartPeer();
            Console.WriteLine("start peer 2");
            _ = peer2.StartPeer();
            Console.WriteLine("all peers started");
            await sources[peer1.ID].Task;
            await sources[peer2.ID].Task;
            Assert.NotEmpty(peer1.Peers);
            Assert.NotEmpty(peer2.Peers);
            while (true)
            {
                if (peer1.Peers.ContainsKey(peer2.ID))
                {
                    await peer1.SetPeer(peer2.ID);
                }
                if (peer1.RmotePeer is not null && peer2.RmotePeer is not null)
                {
                    break;
                }
            }
            await peer1.SetPeer(peer2.ID);
            Console.WriteLine("All peers set");
            await completionSource[peer1.ID].Task;
            await completionSource[peer2.ID].Task;
            Console.WriteLine("all peers connected");
        }

        [Fact(Timeout = 20000)]
        public async Task TestFileTransfer()
        {
            var src = "2.gif";
            var dst = "3.gif";
            peer1 = new Peer(17999, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 15003));
            peer2 = new Peer(35901, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 15003));
            var server = new P2PServer(15003);
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
            await Task.Delay(1000);
            using var md5 = MD5.Create();
            using var fs1 = File.OpenRead(src);
            using var fs2 = File.OpenRead(dst);
            var hash1 = await md5.ComputeHashAsync(fs1);
            var hash2 = await md5.ComputeHashAsync(fs2);
            Assert.True(hash1.SequenceEqual(hash2));
        }

        [Fact(Timeout = 20000)]
        public async Task TestIntegration()
        {
            peer1 = new Peer(9888, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            peer2 = new Peer(9800, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            nums = 0;
            data[peer2.ID] = null;
            var server = new P2PServer(5001);
            server.AddDefaultServerHandler();
            _ = server.StartServerAsync();

            await SetUpPeers();
            Assert.Null(data[peer2.ID]);
            var greetingString = "Hi";
            var hello = new Hello { HelloString = greetingString };
            await peer1.SendDataToPeerAsync(greetingString);
            await peer1.SendDataToPeerAsync(greetingString);
            await peer1.SendDataToPeerAsync(greetingString);
            await Task.Delay(1000);
            Assert.Equal(hello.HelloString, data[peer2.ID]);
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
    }
}