using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using Chronos.P2P.Server;

namespace Chronos.P2P.Client
{
    public class Peer
    {
        public Guid ID { get; }
        UdpClient udpClient;
        PeerEP localEP
            => PeerEP.ParsePeerEPFromIPEP(new IPEndPoint(GetLocalIPAddress(), port));
        int port;
        ConcurrentDictionary<Guid, PeerInfo> peers;
        PeerInfo peer;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        bool peerConnected = false;
        IPEndPoint serverEP;
        P2PServer server;
        public Peer(int port,IPEndPoint serverEP)
        {
            this.serverEP = serverEP;
            ID = Guid.NewGuid();
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            this.port = port;
            server = new P2PServer(udpClient);
            
            

        }
        public void StartPeer()
        {
            StartReceiveData();
            StartBroadCast();
            StartHolePunching();
        }
        public void AddHandlers<T>() where T : new()
            => server.AddHandler<T>();
        Task StartReceiveData()
            => Task.Run(async () =>
            {
                while (true)
                {
                    var re = await udpClient.ReceiveAsync();
                    if (peer is null)
                    {
                        try
                        {
                            peers = JsonSerializer.Deserialize<ConcurrentDictionary<Guid, PeerInfo>>(re.Buffer)!;
                            Console.WriteLine($"Client {ID}: Received data!");
                            foreach (var item in peers)
                            {
                                if (item.Key == ID)
                                {
                                    peers.Remove(ID, out var val);
                                }
                            }
                            Console.WriteLine($"Client {ID}: found peers!");
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        var data = Encoding.Default.GetString(re.Buffer);
                        if (data == "Connected")
                        {
                            Console.WriteLine("Peer connected");
                            peerConnected = true;
                            break;
                        }
                        else if (data == "Hello")
                        {
                            tokenSource.Cancel();
                            Console.WriteLine($"Client {ID}: Received peer message: {data}");
                            Console.WriteLine("Connected!");
                        }

                    }
                }
                await server.StartServerAsync();
            }, tokenSource.Token);
        Task StartHolePunching()
            => Task.Run(async () =>
            {
                while (true)
                {
                    if (peer is not null)
                    {
                        if (tokenSource.IsCancellationRequested)
                        {
                            var data = Encoding.Default.GetBytes("Connected");
                            await udpClient.SendAsync(data, data.Length, peer.OuterEP.ToIPEP());
                            if (peerConnected)
                            {
                                break;
                            }
                        }
                        else
                        {
                            var data = Encoding.Default.GetBytes("Hello");
                            await udpClient.SendAsync(data, data.Length, peer.OuterEP.ToIPEP());
                        }
                        await Task.Delay(1000);
                        continue;
                    }
                    await Task.Delay(1000);
                }
            }, tokenSource.Token);
        Task StartBroadCast()
            => Task.Run(async () =>
            {
                var peerInfo = new PeerInfo { Id = ID, InnerEP = localEP };
                while (true)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    if (peer is null)
                    {
                        peerInfo.NeedData = true;
                    }
                    else
                    {
                        peerInfo.NeedData = false;
                    }
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<PeerInfo>
                    {
                        Method = (int)CallMethods.Connect,
                        Data = peerInfo,
                    });
                    var st = udpClient.SendAsync(bytes, bytes.Length, serverEP);
                    Console.WriteLine($"Client {ID}: Sent connection data!");
                    await Task.Delay(1000, tokenSource.Token);
                }
            }, tokenSource.Token);
        public void SetPeer(Guid id)
        {
            peer = peers[id];
        }
        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}
