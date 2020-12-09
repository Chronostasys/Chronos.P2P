﻿using Chronos.P2P.Client;
using Chronos.P2P.Server;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
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
        private TaskCompletionSource completionSource = new();
        private TaskCompletionSource getPeerCompletionSource = new();
        internal static string data;
        internal static int nums;

        private void Peer_PeerConnected(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                completionSource.TrySetResult();
            });
        }

        private void Peer_PeersDataReceiveed(object sender, EventArgs e)
        {
            Console.WriteLine("Peer1_PeersDataReceiveed called");
            var p = sender as Peer;
            if (!p.peers.IsEmpty)
            {
                getPeerCompletionSource.TrySetResult();
            }
        }

        private async Task SetUpPeers(Peer peer1, Peer peer2)
        {
            peer1.PeersDataReceiveed += Peer_PeersDataReceiveed;
            peer2.PeersDataReceiveed += Peer_PeersDataReceiveed;
            peer1.PeerConnected += Peer_PeerConnected;
            peer2.PeerConnected += Peer_PeerConnected;

            peer1.AddHandlers<ClientHandler>();
            peer2.AddHandlers<ClientHandler>();

            _ = peer1.StartPeer();
            _ = peer2.StartPeer();

            await getPeerCompletionSource.Task;
            while (true)
            {
                if (peer1.peers is not null && peer1.peers.ContainsKey(peer2.ID))
                {
                    peer1.SetPeer(peer2.ID);
                    break;
                }
                await Task.Delay(100);
            }
            while (true)
            {
                if (peer2.peers is not null && peer2.peers.ContainsKey(peer1.ID))
                {
                    peer2.SetPeer(peer1.ID);
                    break;
                }
                await Task.Delay(100);
            }
            await completionSource.Task;
        }

        [Fact(DisplayName = "File Transfer test", Timeout = 20000)]
        public async Task TestFileTransfer()
        {
            var src = "Tommee Profitt,Jung Youth,Fleurie - In the End.mp3";
            var dst = "transfered.mp3";
            var peer1 = new Peer(10999, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            var peer2 = new Peer(10901, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            peer1.OnInitFileTransfer = info =>
            {
                return Task.FromResult((true, dst));
            };
            peer2.OnInitFileTransfer = info =>
            {
                return Task.FromResult((true, dst));
            };
            await SetUpPeers(peer1, peer2);
            await peer1.SendFileAsync(src);
            await Task.Delay(1000);
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
            var peer1 = new Peer(9888, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            var peer2 = new Peer(9800, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            var server = new P2PServer(5001);
            server.AddDefaultServerHandler();
            _ = server.StartServerAsync();

            await SetUpPeers(peer1, peer2);
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
            Assert.Equal(1000, nums);
            peer1.Dispose();
            peer2.Dispose();

            server.Dispose();
        }

        [Fact(DisplayName = "Remote Server Integration test", Timeout = 20000)]
        public async Task TestRemoteIntegration()
        {
            data = null;
            var peer1 = new Peer(9999, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            var peer2 = new Peer(9901, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));

            await SetUpPeers(peer1, peer2);
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