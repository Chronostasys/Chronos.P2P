using Chronos.P2P.Server;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Chronos.P2P.Client.Utils;
using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.IO.MemoryMappedFiles;

namespace Chronos.P2P.Client
{
    public class Peer : IRequestHandlerCollection, IDisposable
    {
        #region Fields

        private readonly TaskCompletionSource<bool> connectionHandshakeTask
            = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly object epKey = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> FileAcceptTasks = new();
        private readonly CancellationTokenSource lifeTokenSource = new();
        private readonly P2PServer server;
        private readonly IPEndPoint serverEP;
        private readonly CancellationTokenSource tokenSource = new();
        private readonly UdpClient udpClient;
        private long currentHead = -1;
        private volatile bool isInSameSubNet = false;
        private DateTime lastConnectDataSentTime;
        private DateTime lastPunchDataSentTime;
        private int pingCount = 10;
        internal const int bufferLen = 1400;
        internal volatile bool epConfirmed = false;
        internal ConcurrentDictionary<Guid, FileRecvDicData> FileRecvDic = new();
        internal Stream? fs;
        internal PeerInfo? peer;
        internal ConcurrentDictionary<DataSliceInfo, DataSlice> slices = new();

        #endregion Fields

        #region Delegates & Events

        public Func<BasicFileInfo, Task<(bool receive, string savePath)>>? OnInitFileTransfer;
        public Func<PeerInfo, bool>? OnPeerInvited;

        public event EventHandler? PeerConnected;

        public event EventHandler? PeerConnectionLost;

        public event EventHandler? PeersDataReceived;

        #endregion Delegates & Events

        #region Properties

        private MsgQueue<UdpMsg> Msgs => server.msgs;
        public Guid ID { get; }
        public bool IsPeerConnected { get; private set; } = false;
        public IEnumerable<PeerInnerEP> LocalEP { get; }
        public string? Name { get; }
        public PeerEP? OuterEp { get; private set; }
        public ConcurrentDictionary<Guid, PeerInfo>? Peers { get; private set; }
        public int Port { get; }
        public PeerInfo? RmotePeer => peer;

        #endregion Properties

        public Peer(int port, IPEndPoint serverEP, string? name = null)
        {
            Name = name;
            this.serverEP = serverEP;
            ID = Guid.NewGuid();
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            this.Port = port;
            LocalEP = GetEps();
            server = new P2PServer(udpClient);
            server.AddDefaultServerHandler();
            server.AddHandler<PeerDefaultHandlers>();
            server.services.AddSingleton(this);
            server.AfterDataHandled += (s, e) => ResetPingCount();
            server.OnError += Server_OnError;
            lastPunchDataSentTime = DateTime.UtcNow;
            lastConnectDataSentTime = DateTime.UtcNow;
        }

        #region Workers

        private Task StartBroadCast()
            => Task.Run(async () =>
            {
                var peerInfo = new PeerInfo { Id = ID, InnerEP = LocalEP.ToList() };
                var bytes = P2PServer.CreateUdpRequestBuffer((int)CallMethods.Connect, Guid.Empty, peerInfo);
                while (true)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    if (IsPeerConnected || peer is not null)
                    {
                        break;
                    }
                    Msgs.Enqueue(new UdpMsg
                    {
                        Data = bytes,
                        Ep = serverEP
                    });
                    await Task.Delay(1000, tokenSource.Token);
                }
            });

        private Task StartPing()
            => Task.Run(async () =>
            {
                while (true)
                {
                    if (!IsPeerConnected)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
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
                    if (!IsPeerConnected)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
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
                            Peers = MessagePackSerializer
                                .Deserialize<ConcurrentDictionary<Guid, PeerInfo>>(re.Buffer);
                            foreach (var item in Peers)
                            {
                                if (item.Key == ID)
                                {
                                    Peers.Remove(ID, out var val);
                                    OuterEp = val!.OuterEP;
                                }
                            }
                            // just fire and forget
                            _ = Task.Run(() =>
                            {
                                PeersDataReceived?.Invoke(this, new EventArgs());
                            });
                        }
                        catch (Exception)
                        {
                            await server.ProcessRequestAsync(new UdpReceiveResult(re.Buffer, re.RemoteEndPoint));
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                await Task.WhenAll(server.StartServerAsync(), StartPing(), StartPingWaiting());
            });

        internal Task StartHolePunching()
            => Task.Run(async () =>
            {
                List<PeerEP> eps = new();
                foreach (var item in LocalEP)
                {
                    foreach (var item1 in peer!.InnerEP)
                    {
                        try
                        {
                            if (item.IsInSameSubNet(item1))
                            {
                                eps.Add(item1);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                lock (epKey)
                {
                    if ((peer!.OuterEP.IP == OuterEp!.IP || isInSameSubNet) && !epConfirmed && eps.Count != 0)
                    {
                        try
                        {
                            peer.OuterEP = eps.Where(ep => ep.IP.StartsWith("192")).First();
                            eps.Remove(peer.OuterEP);
                        }
                        catch (Exception)
                        {
                            peer.OuterEP = eps[0];
                            eps.Remove(peer.OuterEP);
                        }
                    }
                }
                int i = 1;
                while (true)
                {
                    lock (epKey)
                    {
                        var prevEp = peer!.OuterEP;
                        if (i > 5 && !epConfirmed && eps.Count != 0)
                        {
                            i = 0;
                            try
                            {
                                peer.OuterEP = eps.Where(ep => ep.IP.StartsWith("192")).First();
                                eps.Remove(peer.OuterEP);
                            }
                            catch (Exception)
                            {
                                peer.OuterEP = eps[0];
                                eps.Remove(peer.OuterEP);
                            }
                            goto punch;
                        }
                    punch:
                        if (!epConfirmed && prevEp == peer.OuterEP && i == 0)
                        {
                            peer.OuterEP = peer.InnerEP.First();
                            peer.InnerEP.Remove(peer.InnerEP.First());
                        }
                        Console.WriteLine($"trying new ep {peer.OuterEP}");
                    }
                    if (peer is not null)
                    {
                        if (IsPeerConnected)
                        {
                            break;
                        }
                        if (tokenSource.IsCancellationRequested)
                        {
                            Console.WriteLine($"Connected data sent to peer {peer.OuterEP.ToIPEP()}");
                            await SendDataToPeerAsync((int)CallMethods.Connected, "");
                            if (IsPeerConnected)
                            {
                                break;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Punching data sent to peer {peer.OuterEP.ToIPEP()}");
                            await SendDataToPeerAsync((int)CallMethods.PunchHole, "");
                            i++;
                        }
                        await Task.Delay(500);
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
                }
            });

        #endregion Workers

        #region File Transfer

        internal async Task FileDataReceived(DataSlice dataSlice)
        {
            if (!FileRecvDic.ContainsKey(dataSlice.SessionId))
            {
                throw new InvalidOperationException("The file transfer session doesn't exist!");
            }
            var semaphoreSlim = FileRecvDic[dataSlice.SessionId].Semaphore;

            if (dataSlice.No == 0)
            {
                FileRecvDic[dataSlice.SessionId].Watch.Start();
                currentHead = -1;
            }

            async Task CleanUpAsync()
            {
                slices.Clear();
                currentHead = -1;
                Console.WriteLine("\nWaiting for io to complete...");
                try
                {
                    FileRecvDic[dataSlice.SessionId].Accessor.Dispose();
                    FileRecvDic[dataSlice.SessionId].Mmf.Dispose();
                    await FileRecvDic[dataSlice.SessionId].FS.DisposeAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                count = 0;
                fs = null;
                FileRecvDic.TryRemove(dataSlice.SessionId, out var val);
                val.Semaphore.Dispose();
                semaphoreSlim = null;
                Console.WriteLine("transfer done!");
                val.Watch.Stop();
                Console.WriteLine($"Time eplased: {val.Watch.Elapsed.TotalSeconds}s");
                Console.WriteLine($"Speed: {val.Length / val.Watch.Elapsed.TotalSeconds / 1024 / 1024}MB/s");
            }
            await ProcessDataSliceAsync(dataSlice, CleanUpAsync);
        }

        internal void OnStreamHandshakeResult(UdpContext context)
        {
            var data = context.GetData<FileTransferHandShakeResult>();
            FileAcceptTasks[data.SessionId].SetResult(data.Accept);
        }

        internal async Task ProcessDataSliceAsync(DataSlice dataSlice, Func<Task> cleanUpAsync)
        {
            var data = FileRecvDic[dataSlice.SessionId];
            data.Accessor.WriteArray(dataSlice.No * bufferLen, dataSlice.Slice, 0, dataSlice.Len);
            if (dataSlice.No%(10240)==0)
            {
                data.Accessor.Flush();
                Console.WriteLine($"data slice no: {dataSlice.No}");
            }
            if (Interlocked.Increment(ref count)==data.Total)
            {
                await cleanUpAsync();
            }
        }
        long count = 0;

        internal async Task StreamTransferRequested(BasicFileInfo data)
        {
            var (recv, savepath) = await (OnInitFileTransfer ??
                (async (info) => await Task.FromResult((true, info.Name)))).Invoke(data);
            var sessionId = data.SessionId;
            if (recv)
            {
                if (data.Length > 0)
                {
                    var mfs = File.Create(savepath);
                    mfs.SetLength(data.Length);
                    var mmf = MemoryMappedFile.CreateFromFile(mfs, null,
                        0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.Inheritable, false);
                    FileRecvDic[sessionId] = new FileRecvDicData
                    {
                        SavePath = savepath,
                        Semaphore = new SemaphoreSlim(1),
                        Length = data.Length,
                        Watch = new Stopwatch(),
                        Mmf = mmf,
                        Accessor = mmf.CreateViewAccessor(),
                        FS = mfs,
                        Total = data.Length / bufferLen + 1
                    };
                    count = 0;
                }
            }
            await SendDataToPeerReliableAsync((int)CallMethods.StreamHandShakeCallback, new FileTransferHandShakeResult
            {
                Accept = recv,
                SessionId = data.SessionId
            });
        }

        public static byte[] SliceToBytes(bool last, int len, long no, Guid sessionId, Memory<byte> slice)
        {
            var bytes = new byte[len + 29];
            Span<byte> dataSpan = bytes;
            MemoryMarshal.Write(dataSpan, ref last);
            MemoryMarshal.Write(dataSpan[1..], ref len);
            MemoryMarshal.Write(dataSpan[5..], ref no);
            MemoryMarshal.Write(dataSpan[13..], ref sessionId);
            Memory<byte> mem = bytes;
            slice[0..len].CopyTo(mem[29..]);
            return bytes;
        }

        /// <summary>
        /// Send file to a peer.
        /// This method is capable to handle large files
        /// </summary>
        /// <param name="location">Path of the file</param>
        /// <param name="concurrentLevel">As it's name. However,
        /// concurrent level doen't exactly mean thread nums.
        /// And a higher concurrent level may result in higher packet loss rate.
        /// So adjust it carefully to fit your need.</param>
        /// <returns></returns>
        public async Task SendFileAsync(string location, int concurrentLevel = 3)
        {
            using SemaphoreSlim semaphore = new(concurrentLevel);
            using var fs = File.OpenRead(location);
            var sessionId = Guid.NewGuid();
            FileAcceptTasks[sessionId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendDataToPeerReliableAsync((int)CallMethods.StreamHandShake, new BasicFileInfo
            {
                Length = fs.Length,
                Name = Path.GetFileName(fs.Name),
                SessionId = sessionId
            });
            var acc = await FileAcceptTasks[sessionId].Task;
            if (!acc)
            {
                throw new OperationCanceledException("Remote refused!");
            }
            var cancelSource = new CancellationTokenSource();
            var total = fs.Length / bufferLen;
            Console.WriteLine($"Slice count: {total}");
            Memory<byte> fileReadBuffer = new byte[10240 * bufferLen];
            int readLen = 0;
            for (long i = 0, j = 0; i < fs.Length; i += bufferLen, j++)
            {
                int n = (int)(j % 10240);
                if (n==0)
                {
                    var rt = fs.ReadAsync(fileReadBuffer);
                    await semaphore.WaitAsync();
                    readLen = await rt;
                }
                else
                {
                    await semaphore.WaitAsync();
                }
                var buffer = fileReadBuffer[(n * bufferLen)..((n + 1) * bufferLen)];

                var l = i >= fs.Length - bufferLen;
                var len = l ? (readLen - n * bufferLen) : bufferLen;
                cancelSource.Token.ThrowIfCancellationRequested();
                var j1 = j;

                if (l)
                {
                    var excr = await SendDataToPeerReliableAsync((int)CallMethods.DataSlice,
                        SliceToBytes(l, len, j1, sessionId, buffer),
                        30, cancelSource.Token);
                    semaphore.Release();
                    if (!excr)
                    {
                        cancelSource.Cancel();
                    }
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        var excr = await SendDataToPeerReliableAsync((int)CallMethods.DataSlice,
                            SliceToBytes(l, len, j1, sessionId, buffer),
                            30, cancelSource.Token);
                        semaphore.Release();
                        if (!excr)
                        {
                            cancelSource.Cancel();
                        }
                    });
                }
            }
            Console.WriteLine($"Auto adjusted timeout: {server.timeoutData.SendTimeOut}ms");
            if (semaphore.CurrentCount == concurrentLevel)
            {
                semaphore.Release();
            }
        }

        public async Task SendLiveStreamAsync(MsgQueue<(byte[], int)> channel, string name, int callMethod, CancellationToken token = default)
        {
            var sessionId = Guid.NewGuid();
            FileAcceptTasks[sessionId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendDataToPeerReliableAsync((int)CallMethods.StreamHandShake, new BasicFileInfo
            {
                Length = -1,
                Name = name,
                SessionId = sessionId
            });
            long no = 0;
            await FileAcceptTasks[sessionId].Task;
            await foreach (var (buffer, len) in channel)
            {
                token.ThrowIfCancellationRequested();
                Memory<byte> mem = buffer;
                for (int i = 0; i < len / bufferLen + 1; i++)
                {
                    var left = len - i * bufferLen;
                    var sendLen = ((left < bufferLen) ? left : bufferLen);
                    _ = SendDataReliableAsync(callMethod, SliceToBytes(false, sendLen,
                        no++, sessionId, mem[(i * bufferLen)..(i * bufferLen + sendLen)]), peer!.OuterEP.ToIPEP());
                }
            }
        }

        #endregion File Transfer

        #region Handlers

        private void Server_OnError(object? sender, byte[] e)
        {
            var str = Encoding.Default.GetString(e);
            if (str == "Connected\n")
            {
                Msgs.Enqueue(new UdpMsg
                {
                    Data = e,
                    Ep = peer!.OuterEP.ToIPEP()
                });
            }
        }

        internal void OnConnectionCallback(bool acc)
        {
            connectionHandshakeTask.TrySetResult(acc);
        }

        internal async Task OnConnectionRequested(PeerInfo requester)
        {
            var re = OnPeerInvited?.Invoke(requester);
            if (!re.HasValue || re.Value)
            {
                Console.WriteLine("accept!");
                peer = requester;

                await SendDataReliableAsync((int)CallMethods.ConnectionHandShakeReply, new ConnectionReplyDto
                {
                    Ep = requester.OuterEP,
                    Acc = true
                }, serverEP);
            }
            else
            {
                await SendDataReliableAsync((int)CallMethods.ConnectionHandShakeReply, new ConnectionReplyDto
                {
                    Ep = requester.OuterEP,
                    Acc = false
                }, serverEP);
            }
        }

        internal async void PeerConnectedReceived()
        {
            if (IsPeerConnected
                && (DateTime.UtcNow - lastConnectDataSentTime).TotalMilliseconds > 500)
            {
                await SendDataToPeerAsync((int)CallMethods.Connected, "");
                lastConnectDataSentTime = DateTime.UtcNow;
                return;
            }
            Console.WriteLine("Peer connected");
            IsPeerConnected = true;
            _ = Task.Run(() =>
            {
                PeerConnected?.Invoke(this, new EventArgs());
            });
        }

        internal async void PunchDataReceived(UdpContext context)
        {
            var ep = PeerEP.ParsePeerEPFromIPEP(context.RemoteEndPoint);
            lock (epKey)
            {
                peer!.OuterEP = ep;
                epConfirmed = true;
                Console.WriteLine($"Peer punch data received, set remote ep to {peer.OuterEP}");
            }
            if (tokenSource.IsCancellationRequested
                && (DateTime.UtcNow - lastPunchDataSentTime).TotalMilliseconds > 500)
            {
                lastPunchDataSentTime = DateTime.UtcNow;
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

        #endregion Handlers

        #region Network Helper

        internal IEnumerable<PeerInnerEP> GetEps()
        {
            foreach (var item in GetLocalIPAddress())
            {
                yield return new PeerInnerEP(PeerEP.ParsePeerEPFromIPEP(new IPEndPoint(item, Port)));
            }
        }

        public static IEnumerable<IPAddress> GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            int i = 0;
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    i++;
                    yield return ip;
                }
            }
            if (i == 0)
            {
                throw new Exception("No network adapters with an IPv4 address in the system!");
            }
        }

        public static IPAddress GetSubnetMask(IPAddress address)
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (address.Equals(unicastIPAddressInformation.Address))
                        {
                            return unicastIPAddressInformation.IPv4Mask;
                        }
                    }
                }
            }
            throw new ArgumentException(string.Format("Can't find subnetmask for IP address '{0}'", address));
        }

        #endregion Network Helper

        #region Send Data

        public Task SendDataAsync<T>(int method, T data, IPEndPoint ep)
        {
            var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var bytes = P2PServer.CreateUdpRequestBuffer(method, Guid.Empty, data);
            Msgs.Enqueue(new UdpMsg
            {
                Data = bytes,
                Ep = ep,
                SendTask = t
            });
            return t.Task;
        }

        public virtual ValueTask<bool> SendDataReliableAsync<T>(int method, T data, IPEndPoint ep, int retry = 10, CancellationToken? token = null)
        {
            return server.SendDataReliableAsync(method, data, ep, retry, token);
        }

        public Task SendDataToPeerAsync<T>(T data) where T : class
        {
            return SendDataToPeerAsync((int)CallMethods.P2PDataTransfer, data);
        }

        public Task SendDataToPeerAsync<T>(int method, T data)
        {
            return SendDataAsync(method, data, peer!.OuterEP.ToIPEP());
        }

        public virtual ValueTask<bool> SendDataToPeerReliableAsync<T>(T data, CancellationToken? token = null)
        {
            return SendDataToPeerReliableAsync((int)CallMethods.P2PDataTransfer, data, 3, token);
        }

        public virtual ValueTask<bool> SendDataToPeerReliableAsync<T>(int method, T data, int retry = 10, CancellationToken? token = null)
        {
            return SendDataReliableAsync(method, data, peer!.OuterEP.ToIPEP(), retry, token);
        }

        #endregion Send Data

        #region User Interface

        public static Peer BuildWithStartUp<T>(int port, IPEndPoint serverEP, string? name = null)
                    where T : IStartUp, new()
        {
            var peer = new Peer(port, serverEP, name);
            var startUp = new T();
            startUp.Configure(peer);
            peer.ConfigureServices(startUp.ConfigureServices);
            return peer;
        }

        public void AddHandler<T>() where T : class
            => server.AddHandler<T>();

        public void Cancel()
        {
            lifeTokenSource.Cancel();
        }

        public void ConfigureServices(Action<ServiceCollection> configureAction)
            => server.ConfigureServices(configureAction);

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            udpClient.Dispose();
        }

        /// <summary>
        /// Set target peer
        /// </summary>
        /// <param name="id"></param>
        /// <returns>A task complete when another peer reply our connection request.
        /// <see langword="false"/> when refused, <see langword="true"/> when accepted.</returns>
        public async ValueTask<bool> SetPeer(Guid id, bool isInSameNet = false)
        {
            isInSameSubNet = isInSameNet;
            if (peer is not null)
            {
                return true;
            }
            peer = Peers![id];
            await SendDataReliableAsync((int)CallMethods.ConnectionHandShake, new ConnectHandshakeDto
            {
                Ep = peer.OuterEP,
                Info = new PeerInfo { Id = ID, InnerEP = LocalEP.ToList() }
            }, serverEP);
            var re = await connectionHandshakeTask.Task;
            return re;
        }

        public Task StartPeer()
        {
            var t = StartReceiveData();
            StartBroadCast();
            return t;
        }

        #endregion User Interface
    }
}