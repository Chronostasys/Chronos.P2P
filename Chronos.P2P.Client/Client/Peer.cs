using Chronos.P2P.Server;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public struct DataSlice
    {
        public Guid SessionId { get; set; }
        public int No { get; set; }
        public byte[] Slice { get; set; }
        public int Len { get; set; }
        public bool Last { get; set; }
    }
    public struct BasicFileInfo
    {
        public Guid SessionId { get; set; }
        public string Name { get; set; }
        public long Length { get; set; }
    }
    public struct DataSliceInfo
    {
        public Guid SessionId { get; set; }
        public int No { get; set; }
        public override int GetHashCode()
        {
            unchecked
            {
                int result = 37; // prime

                result *= 397; // also prime (see note)
                result += SessionId.GetHashCode();

                result *= 397;
                result += No.GetHashCode();

                return result;
            }
        }
    }
    public class Peer : IDisposable
    {
        private ConcurrentDictionary<Guid, TaskCompletionSource<bool>> AckTasks
            = new ConcurrentDictionary<Guid, TaskCompletionSource<bool>>();

        private DateTime lastConnectTime = DateTime.UtcNow;
        private DateTime lastPunchTime = DateTime.UtcNow;
        private CancellationTokenSource lifeTokenSource = new CancellationTokenSource();
        private PeerInfo peer;
        private bool peerConnected = false;
        private int pingCount = 10;
        private int port;
        private P2PServer server;
        private IPEndPoint serverEP;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        private UdpClient udpClient;

        public event EventHandler PeerConnected;

        public event EventHandler PeerConnectionLost;

        public event EventHandler PeersDataReceiveed;

        public Guid ID { get; }

        public PeerEP LocalEP
            => PeerEP.ParsePeerEPFromIPEP(new IPEndPoint(GetLocalIPAddress(), port));

        public PeerEP OuterEp { get; private set; }

        public string Name { get; }
        public ConcurrentDictionary<Guid, PeerInfo> peers { get; private set; }

        public Peer(int port, IPEndPoint serverEP, string name = null)
        {
            Name = name;
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
            server.AfterDataHandled += (s, e) => ResetPingCount();
            server.OnError += Server_OnError;
        }

        private async Task<bool> Delay()
        {
            await Task.Delay(1000);
            return false;
        }

        private void Server_OnError(object sender, byte[] e)
        {
            var str = Encoding.Default.GetString(e);
            if (str == "Connected\n")
            {
                udpClient.SendAsync(e, e.Length, peer.OuterEP.ToIPEP());
            }
        }

        private Task StartBroadCast()
            => Task.Run(async () =>
            {
                var peerInfo = new PeerInfo { Id = ID, InnerEP = LocalEP };
                while (true)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    if (peer is not null)
                    {
                        break;
                    }
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<PeerInfo>
                    {
                        Method = (int)CallMethods.Connect,
                        Data = peerInfo,
                    });
                    var st = udpClient.SendAsync(bytes, bytes.Length, serverEP);
                    await Task.Delay(1000, tokenSource.Token);
                }
            });

        private Task StartHolePunching()
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
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
                }
            });

        private Task StartPing()
            => Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(7000, lifeTokenSource.Token);
                    lifeTokenSource.Token.ThrowIfCancellationRequested();
                    await SendDataToPeerReliableAsync((int)CallMethods.P2PPing, "");
                }
            });

        private Task StartPingWaiting()
            => Task.Run(async () =>
            {
                pingCount = 10;
                while (true)
                {
                    await Task.Delay(1000);
                    pingCount--;

                    if (pingCount == 0)
                    {
                        Console.WriteLine("connection lost!");
                        Console.WriteLine($"{ID}");
                        PeerConnectionLost?.Invoke(this, new());
                        peer = null;
                    }
                }
            });

        private Task StartReceiveData()
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
                            foreach (var item in peers)
                            {
                                if (item.Key == ID)
                                {
                                    peers.Remove(ID, out var val);
                                    OuterEp = val.OuterEP;
                                }
                            }
                            // just fire and forget
                            _ = Task.Run(() =>
                            {
                                PeersDataReceiveed?.Invoke(this, new EventArgs());
                            });
                            
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
                await Task.WhenAll(server.StartServerAsync(), StartPing(), StartPingWaiting());
            });

        internal void AckReturned(Guid reqId)
        {
            try
            {
                AckTasks[reqId].TrySetResult(true);
            }
            catch (Exception)
            {
            }
            
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

        internal async void PunchDataReceived()
        {
            if ((DateTime.UtcNow - lastPunchTime).TotalMilliseconds < 500)
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

        internal void ResetPingCount()
        {
            pingCount = 10;
        }

        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = null;
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            return ipAddress??
                throw new Exception("No network adapters with an IPv4 address in the system!");

        }

        public void AddHandlers<T>() where T : class
            => server.AddHandler<T>();

        public void Cancel()
        {
            lifeTokenSource.Cancel();
        }

        public void Dispose()
        {
            udpClient.Dispose();
        }
        public Func<BasicFileInfo, Task<(bool receive, string savePath)>> OnInitFileTransfer;
        internal async Task FileTransferRequested(UdpContext context)
        {
            var data = context.GetData<BasicFileInfo>().Data;
            var (recv, savepath)= await (OnInitFileTransfer??
                (async (info)=>(true, info.Name))).Invoke(data);
            var sessionId = data.SessionId;
            if (recv)
            {
                FileRecvDic[sessionId] = (savepath, new SemaphoreSlim(1));
            }
        }
        ConcurrentDictionary<Guid, (string, SemaphoreSlim)> FileRecvDic = new();
        internal async Task FileDataReceived(UdpContext context)
        {
            var dataSlice = context.GetData<DataSlice>().Data;
            
            if (!FileRecvDic.ContainsKey(dataSlice.SessionId))
            {
                throw new InvalidOperationException("The file transfer session doesn't exist!");
            }
            var semaphoreSlim = FileRecvDic[dataSlice.SessionId].Item2;
            await semaphoreSlim.WaitAsync();
            if (dataSlice.No == 0)
            {
                fs = File.Create(FileRecvDic[dataSlice.SessionId].Item1);
                currentHead = -1;
            }
            if (dataSlice.Last)
            {
                Console.WriteLine("last");
            }
            async Task CleanUpAsync()
            {
                slices.Clear();
                currentHead = -1;
                await fs.DisposeAsync();
                FileRecvDic.TryRemove(dataSlice.SessionId, out var val);
                val.Item2.Dispose();
                Console.WriteLine("transfer done!");
            }

            if (fs is not null && currentHead == dataSlice.No - 1)
            {
                await fs.WriteAsync(dataSlice.Slice, 0, dataSlice.Len);
                currentHead = dataSlice.No;
                if (dataSlice.Last)
                {
                    await CleanUpAsync();
                }
                while (slices.TryGetValue(new DataSliceInfo { No = ++dataSlice.No, SessionId = dataSlice.SessionId }, out var slice))
                {
                    await fs.WriteAsync(slice.Slice, 0, slice.Len);
                    currentHead = dataSlice.No;
                    if (slice.Last)
                    {
                        await CleanUpAsync();
                    }
                }
            }
            else
            {
                slices[new DataSliceInfo { No = dataSlice.No, SessionId = dataSlice.SessionId }] = dataSlice;
            }
            semaphoreSlim.Release();

        }
        int currentHead = -1;
        Stream fs;
        ConcurrentDictionary<DataSliceInfo, DataSlice> slices = new ConcurrentDictionary<DataSliceInfo, DataSlice>();
        const int bufferLen = 10240;
        public async Task SendFileAsync(string location)
        {
            
            using var fs = File.OpenRead(location);
            var sessionId = Guid.NewGuid();
            await SendDataToPeerReliableAsync((int)CallMethods.FileHandShake, new BasicFileInfo 
            {
                Length = fs.Length,
                Name = fs.Name,
                SessionId = sessionId
            });
            var cancelSource = new CancellationTokenSource();
            var buffer = new byte[bufferLen];
            var last = fs.Length / bufferLen;
            for (int i = 0, j = 0; i < fs.Length; i+=bufferLen, j++)
            {
                var len = await fs.ReadAsync(buffer, 0, bufferLen);
                if (i >= fs.Length - bufferLen)
                {
                    Console.WriteLine("last");
                }
                cancelSource.Token.ThrowIfCancellationRequested();
                _ = SendDataToPeerReliableAsync((int)CallMethods.DataSlice,
                    new DataSlice
                    {
                        No = j,
                        Slice = buffer,
                        Len = len,
                        Last = i >= fs.Length - bufferLen
                    }).ContinueWith(async re=>
                    {
                        if(!await re)
                        {
                            cancelSource.Cancel();
                        }
                    });
                //while (!await SendDataToPeerReliableAsync((int)CallMethods.DataSlice,
                //    new DataSlice
                //    {
                //        No = j,
                //        Slice = buffer,
                //        Len = len,
                //        Last = i >= fs.Length - bufferLen
                //    }))
                //{
                //    Console.WriteLine("retry");
                //}
            }
        }

        public Task SendDataToPeerAsync<T>(T data) where T : class
        {
            return SendDataToPeerAsync((int)CallMethods.P2PDataTransfer, data);
        }

        public async Task SendDataToPeerAsync<T>(int method, T data)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<T>
            {
                Method = method,
                Data = data,
            });
            await udpClient.SendAsync(bytes, bytes.Length, peer.OuterEP.ToIPEP());
        }

        public async Task<bool> SendDataToPeerReliableAsync<T>(T data, CancellationToken? token = null)
        {
            return await SendDataToPeerReliableAsync((int)CallMethods.P2PDataTransfer, data, token);
        }

        public async Task<bool> SendDataToPeerReliableAsync<T>(int method, T data, CancellationToken? token = null)
        {
            var reqId = Guid.NewGuid();
            AckTasks[reqId] = new TaskCompletionSource<bool>();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<T>
            {
                Method = method,
                Data = data,
                ReqId = reqId
            });
            for (int i = 0; i < 3; i++)
            {
                token?.ThrowIfCancellationRequested();
                await udpClient.SendAsync(bytes, bytes.Length, peer.OuterEP.ToIPEP());
                var t = await await Task.WhenAny(AckTasks[reqId].Task, Delay());
                if (t)
                {
                    AckTasks.TryRemove(reqId, out var completionSource);
                    return true;
                }
            }

            AckTasks.TryRemove(reqId, out var taskCompletionSource);
            return false;
        }

        public void SetPeer(Guid id)
        {
            peer = peers[id];
            // 自动切换至局域网内连接
            if (peer.OuterEP.IP==OuterEp.IP)
            {
                peer.OuterEP = peer.InnerEP;
            }
        }

        public Task StartPeer()
        {
            var t = StartReceiveData();
            StartBroadCast();
            StartHolePunching();
            return t;
        }
    }
}