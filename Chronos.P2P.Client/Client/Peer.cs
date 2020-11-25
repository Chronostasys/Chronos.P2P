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
using Chronos.P2P.Client;

namespace Chronos.P2P.Client
{
    public class Peer:IDisposable
    {
        public Guid ID { get; }
        UdpClient udpClient;
        PeerEP localEP
            => PeerEP.ParsePeerEPFromIPEP(new IPEndPoint(GetLocalIPAddress(), port));
        int port;
        public ConcurrentDictionary<Guid, PeerInfo> peers { get; private set; }
        PeerInfo peer;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        bool peerConnected = false;
        IPEndPoint serverEP;
        P2PServer server;
        public event EventHandler PeersDataReceiveed;
        public event EventHandler PeerConnected;
        public Peer(int port,IPEndPoint serverEP)
        {
            this.serverEP = serverEP;
            ID = Guid.NewGuid();
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            this.port = port;
            server = new P2PServer(udpClient);
            server.ConfigureServices(services =>
            {

            });

        }
        public Task StartPeer()
        {
            var t = StartReceiveData();
            StartBroadCast();
            StartHolePunching();
            return t;
        }
        public void AddHandlers<T>() where T : class
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
                            PeersDataReceiveed?.Invoke(this, new EventArgs());
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
                            PeerConnected?.Invoke(this, new EventArgs());
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
            });
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
                            await Task.Delay(100);
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
        public async Task SendDataToPeerAsync<T>(T data) where T:class
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<T>
            {
                Method = (int)CallMethods.P2PDataTransfer,
                Data = data,
            });
            await udpClient.SendAsync(bytes, bytes.Length, peer.OuterEP.ToIPEP());
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

        public void Dispose()
        {
            udpClient.Dispose();
        }
    }
}
