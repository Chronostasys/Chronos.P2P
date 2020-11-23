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
        CancellationTokenSource source = new CancellationTokenSource();
        public Peer(int port,IPEndPoint serverEP)
        {
            ID = Guid.NewGuid();
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            this.port = port;
            Task.Run(async () =>
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
                            peer = peers.Values.First();
                            Console.WriteLine($"Client {ID}: found peer!");
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        var data = Encoding.Default.GetString(re.Buffer);
                        if (data.Length>10)
                        {
                            continue;
                        }
                        Console.WriteLine($"Client {ID}: Received peer message: {data}");
                        Console.WriteLine("Connected!");
                        source.Cancel();
                    }
                }
            }, source.Token);
            Task.Run(async () =>
            {
                var peerInfo = new PeerInfo { Id = ID, InnerEP = localEP };
                while (true)
                {
                    source.Token.ThrowIfCancellationRequested();
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<PeerInfo>
                    {
                        Method = ServerMethods.Connect,
                        Data = peerInfo
                    });
                    var st = udpClient.SendAsync(bytes, bytes.Length, serverEP);
                    Console.WriteLine($"Client {ID}: Sent connection data!");
                    await Task.Delay(1000, source.Token);
                }
            }, source.Token);
            Task.Run(async () =>
            {
                while (true)
                {
                    //source.Token.ThrowIfCancellationRequested();
                    if (peer is not null)
                    {
                        var data = Encoding.Default.GetBytes("Hello");
                        await udpClient.SendAsync(data, data.Length, peer.OuterEP.ToIPEP());
                        await Task.Delay(1000);
                        continue;
                    }
                    await Task.Delay(1000);
                }
            }, source.Token);

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
