﻿using Chronos.P2P.Client;
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
        TaskCompletionSource getPeerCompletionSource = new();

        private void Peer_PeerConnected(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                completionSource.SetResult();
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

        async Task TestConnection(Peer peer1, Peer peer2)
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
                if (!peer1.peers.IsEmpty && peer1.peers.ContainsKey(peer2.ID))
                {
                    peer1.SetPeer(peer2.ID);
                    break;
                }
                await Task.Delay(100);
            }
            while (true)
            {
                if (!peer2.peers.IsEmpty && peer2.peers.ContainsKey(peer1.ID))
                {
                    peer2.SetPeer(peer1.ID);
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
            for (int i = 0; i < 1000; i++)
            {
                await peer1.SendDataToPeerReliableAsync("test");
            }
            Assert.Equal(1000, nums);
            peer1.Dispose();
            peer2.Dispose();
        }

        [Fact(DisplayName = "Local Server Integration test", Timeout = 60000)]
        public async Task TestIntegration()
        {
            nums = 0;
            data = null;
            var peer1 = new Peer(9888, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            var peer2 = new Peer(9800, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001));
            var server = new P2PServer(5001);
            server.AddDefaultServerHandler();
            _ = server.StartServerAsync();

            await TestConnection(peer1, peer2);

            server.Dispose();
        }

        [Fact(DisplayName = "Remote Server Integration test", Timeout = 60000)]
        public async Task TestRemoteIntegration()
        {
            data = null;
            var peer1 = new Peer(9999, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            var peer2 = new Peer(9901, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));

            await TestConnection(peer1, peer2);
        }
    }
}