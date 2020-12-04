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
using Microsoft.Extensions.DependencyInjection;

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
        DateTime lastPunchTime = DateTime.UtcNow;
        DateTime lastConnectTime = DateTime.UtcNow;
        public Peer(int port,IPEndPoint serverEP)
        {
            this.serverEP = serverEP;
            ID = Guid.NewGuid();
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            this.port = port;
            server = new P2PServer(udpClient);
            server.AddHandler<PeerDefaultHandlers>();
            server.ConfigureServices(services =>
            {
                services.AddSingleton(this);
            });
            server.OnError += Server_OnError;

        }
        internal async void PunchDataReceived()
        {
            if ((DateTime.UtcNow-lastPunchTime).TotalMilliseconds<500)
            {
                return;
            }
            if (tokenSource.IsCancellationRequested)
            {
                await SendDataToPeerAsync((int)CallMethods.PunchHole, "");
                return;
            }
            tokenSource.Cancel();
            Console.WriteLine("Connected!");
        }
        internal async void PeerConnectedReceived()
        {
            if ((DateTime.UtcNow - lastConnectTime).TotalMilliseconds < 500)
            {
                return;
            }
            if (peerConnected)
            {
                await SendDataToPeerAsync((int)CallMethods.Connected, "");
                return;
            }
            Console.WriteLine("Peer connected");
            peerConnected = true;
            PeerConnected?.Invoke(this, new EventArgs());
        }
        private void Server_OnError(object sender, byte[] e)
        {
            var str = Encoding.Default.GetString(e);
            if (str == "Connected\n")
            {
                udpClient.SendAsync(e, e.Length, peer.OuterEP.ToIPEP());
            }
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
                        break;
                        

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
                        if (peerConnected)
                        {
                            break;
                        }
                        if (tokenSource.IsCancellationRequested)
                        {
                            await SendDataToPeerAsync((int)CallMethods.Connected, "");
                            if (peerConnected)
                            {
                                break;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Punching data sent to peer {peer.OuterEP.ToIPEP()}");
                            await SendDataToPeerAsync((int)CallMethods.PunchHole, "");
                        }
                        await Task.Delay(500);
                        continue;
                    }
                    await Task.Delay(1000);
                }
            });
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
                        break;
                    }
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<PeerInfo>
                    {
                        Method = (int)CallMethods.Connect,
                        Data = peerInfo,
                    });
                    var st = udpClient.SendAsync(bytes, bytes.Length, serverEP);
                    Console.WriteLine($"Client: Sent connection data!");
                    await Task.Delay(1000, tokenSource.Token);
                }
            });
        public void SetPeer(Guid id)
        {
            peer = peers[id];
        }
        public Task SendDataToPeerAsync<T>(T data) where T:class
        {
            return SendDataToPeerAsync((int)CallMethods.P2PDataTransfer, data);
        }
        public async Task SendDataToPeerAsync<T>(int method, T data) where T : class
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<T>
            {
                Method = method,
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
