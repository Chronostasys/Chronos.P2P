﻿using Chronos.P2P.Client;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Chronos.P2P.Server.Sample
{
    internal class Program
    {
        private static TaskCompletionSource completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static TaskCompletionSource connectionCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static int nums;

        private static async Task Main(string[] args)
        {
            bool server = false;
            if (server)
            {
                await StartServerAsync();
            }
            else
            {
                Console.WriteLine("enter your port:");

                var p = int.Parse(Console.ReadLine());
                var peer = new Peer(p, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
                peer.PeersDataReceived += Peer1_PeersDataReceived;
                peer.PeerConnected += Peer1_PeerConnected;
                peer.AddHandlers<ClientHandler>();
                _ = peer.StartPeer();

                //peer1.StartPeer();

                Console.WriteLine($"your peer id: {peer.ID}");
                Console.WriteLine("Waiting for handshake server...");
                await completionSource.Task;
                Console.WriteLine("Enter the peer id you would like to communicate to (press enter to see available peer list):");
                Guid id;
                while (!Guid.TryParse(Console.ReadLine(), out id))
                {
                    foreach (var item in peer.peers)
                    {
                        Console.WriteLine($"peer id: {item.Key}, innerip: {item.Value.InnerEP}, outerip: {item.Value.OuterEP}");
                    }
                    Console.WriteLine("Enter the peer id you would like to communicate to (press enter to see available peer list):");
                }
                peer.SetPeer(id, true);
                Console.WriteLine("Waiting for connection to establish...");
                await connectionCompletionSource.Task;
                Console.Clear();
                Console.WriteLine("Peer connectd!");
                while (true)
                {
                    await peer.SendFileAsync(Console.ReadLine());
                }
            }
        }

        private static void Peer_PeerConnectionLost(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void Peer1_PeerConnected(object sender, EventArgs e)
        {
            connectionCompletionSource.TrySetResult();
        }

        private static void Peer1_PeersDataReceived(object sender, EventArgs e)
        {
            var a = completionSource.TrySetResult();

            return;
        }

        private static async Task StartServerAsync()
        {
            var server = new P2PServer();
            server.AddDefaultServerHandler();
            await server.StartServerAsync();
        }
    }

    public class ClientHandler
    {
        [Handler((int)CallMethods.P2PDataTransfer)]
        public void OnReceiveData(UdpContext udpContext)
        {
            var d = udpContext.GetData<string>().Data;
            if (d == "test")
            {
                Program.nums++;
            }
            Console.WriteLine("peer:" + d);
        }
    }
}