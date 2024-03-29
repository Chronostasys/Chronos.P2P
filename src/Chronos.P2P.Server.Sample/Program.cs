﻿using Chronos.P2P.Client;
using Chronos.P2P.Client.Audio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Chronos.P2P.Server.Sample
{
    internal class Program
    {
        private static readonly TaskCompletionSource completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static readonly TaskCompletionSource connectionCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static int nums;

        private static async Task Main(string[] args)
        {
            bool first = true;
            bool server = false;
            bool audio = false;
            if (server)
            {
                await StartServerAsync();
            }
            else
            {
                Console.WriteLine("enter your port:");

                var p = int.Parse(Console.ReadLine());
                var peer = new Peer(p, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000)); /*new Peer(p, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));*/
                peer.PeersDataReceived += Peer1_PeersDataReceived;
                peer.PeerConnected += Peer1_PeerConnected;
                peer.AddHandler<ClientHandler>();
                peer.AddHandler<AudioLiveStreamHandler>();
                peer.AddHandler<FSHandler>();
                peer.ConfigureServices(services =>
                {
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                    });
                });
                peer.FileReceiveProgressInvoker = (p) =>
                {
                    var empty = 20 - (int)(p.Percent / 5);
                    var bar = "[" + new String('#', (int)(p.Percent / 5)) + new String(' ', empty) + "]";
                    Console.Write($"\r {bar} {(int)p.Percent}%");
                };
                peer.OnPeerInvited = (p) =>
                {
                    if (first)
                    {
                        first = false;
                        return true;
                    }

                    return false;
                };
                _ = peer.StartPeer();

                //peer1.StartPeer();

                Console.WriteLine($"your peer id: {peer.ID}");
                Console.WriteLine("Waiting for handshake server...");
                await completionSource.Task;
                Console.WriteLine("Enter the peer id you would like to communicate to (press enter to see available peer list):");
                Guid id;
                while (!Guid.TryParse(Console.ReadLine(), out id))
                {
                    if (peer.IsPeerConnected)
                    {
                        goto connected;
                    }
                    foreach (var item in peer.Peers)
                    {
                        Console.WriteLine($"peer id: {item.Key}, \ninnerip: {string.Join("\n", item.Value.InnerEP)}, \nouterip: {item.Value.OuterEP}");
                    }
                    Console.WriteLine("Enter the peer id you would like to communicate to (press enter to see available peer list):");
                }
                await peer.SetPeer(id);
                Console.WriteLine("Waiting for connection to establish...");
                await connectionCompletionSource.Task;
            connected:
                Console.Clear();
                Console.WriteLine("Peer connectd!");
                if (audio)
                {
                    Console.WriteLine("press enter to start live chat");
                    Console.ReadLine();
                    await peer.StartSendLiveAudio("");
                    Console.ReadLine();
                }
                else
                {
                    while (true)
                    {
                        var cmd = Console.ReadLine();
                        if (cmd.StartsWith("ls"))
                        {
                            await peer.SendDataToPeerReliableAsync((int)Command.LS, cmd.Split(' ')[1]);
                        }
                        else if (cmd.StartsWith("download"))
                        {
                            var f = cmd.Split(' ')[2];
                            peer.OnInitFileTransfer = (file) =>
                            {
                                return Task.FromResult((true, f));
                            };
                            await peer.SendDataToPeerReliableAsync((int)Command.DOWNLOAD, cmd.Split(' ')[1]);
                        }
                        else if (cmd.StartsWith("upload"))
                        {
                            var f = cmd.Split(' ')[1];
                            await peer.SendFileAsync(f, progressInvoker: (p) =>
                            {
                                var empty = 20 - (int)(p.Percent / 5);
                                var bar = "[" + new String('#', (int)(p.Percent / 5)) + new String(' ', empty) + "]";
                                Console.Write($"\r {bar} {(int)p.Percent}%");
                            });
                            Console.WriteLine("\r [####################] 100%");
                            Console.WriteLine("upload complete");
                        }
                    }
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
            Console.WriteLine("Peer connected! Press enter to continue");
        }

        private static void Peer1_PeersDataReceived(object sender, EventArgs e)
        {
            completionSource.TrySetResult();

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
            var d = udpContext.GetData<string>();
            if (d == "test")
            {
                Program.nums++;
            }
            Console.WriteLine("peer:" + d);
        }
    }
    enum Command
    {
        LS = 3000,
        LSRESP,
        DOWNLOAD
    }


    class FSHandler
    {
        private readonly Peer peer;
        ILogger<PeerDefaultHandlers> _logger;

        public FSHandler(Peer peer, ILogger<PeerDefaultHandlers> logger)
        {
            this.peer = peer;
            _logger = logger;
        }

        [Handler((int)Command.LSRESP)]
        public void LSRespHandler(UdpContext context)
        {
            var path = context.GetData<string>();
            Console.WriteLine($"{path}");
            context.Dispose();
        }
    }
}