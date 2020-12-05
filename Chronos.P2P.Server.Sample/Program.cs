﻿using Chronos.P2P.Client;
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
            if (d=="test")
            {
                Program.nums++;
            }
            //Console.WriteLine(d);
        }
    }
    class Program
    {
        public static int nums;
        static async Task Main(string[] args)
        {
            var peer = new Peer(8899, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000),"peer");
            var peer1 = new Peer(8890, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000), "peer1");
            var server = new P2PServer();
            server.AddDefaultServerHandler();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
            server.StartServerAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
            //var t1 = peer.StartPeer();
            //var t2 = peer1.StartPeer();
            //var peer = new Peer(26900, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            //var peer1 = new Peer(8890, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            peer.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            peer1.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            peer1.PeerConnected += Peer1_PeerConnected;
            peer.PeerConnected += Peer1_PeerConnected;
            peer.AddHandlers<ClientHandler>();
            peer1.AddHandlers<ClientHandler>();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
            peer.StartPeer();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
            peer1.StartPeer();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
            Console.WriteLine($"peer: {peer.ID}\npeer1:{peer1.ID}");
            //peer.PeerConnectionLost += Peer_PeerConnectionLost;
            //Task.Run(async () =>
            //{
            //    await Task.Delay(10000);
            //    peer1.Cancel();
            //});
            while (true)
            {
                Console.ReadLine();
                nums = 0;
                var s = "test";
                for (int i = 0; i < 1000; i++)
                {
                    await peer.SendDataToPeerAsync(s);
                }
                
                Console.ReadLine();
                Console.WriteLine(nums);
                Console.ReadLine();
                nums = 0;
                for (int i = 0; i < 1000; i++)
                {
                    await peer.SendDataToPeerReliableAsync(s);
                }
                Console.WriteLine(nums);
                Console.ReadLine();
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
            if (!p.peers.IsEmpty)
            {
                p.SetPeer(p.peers.Keys.First());
            }

        }
    }
}
