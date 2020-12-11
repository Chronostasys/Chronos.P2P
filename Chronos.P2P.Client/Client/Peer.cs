using Chronos.P2P.Server;
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
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public class Peer : IDisposable
    {
        #region Fields

        private const int avgNum = 1000;
        private ConcurrentDictionary<Guid, TaskCompletionSource<bool>> AckTasks = new();
        private long currentHead = -1;
        private ConcurrentDictionary<Guid, TaskCompletionSource<bool>> FileAcceptTasks = new();
        private DateTime lastConnectTime = DateTime.UtcNow;
        private DateTime lastPunchTime = DateTime.UtcNow;
        private CancellationTokenSource lifeTokenSource = new();
        private MsgQueue<UdpMsg> msgs = new();
        private PeerInfo? peer;
        private int pingCount = 10;
        private ConcurrentQueue<long> rtts = new();
        private int sendTimeOut = 1000;
        private P2PServer server;
        private IPEndPoint serverEP;
        private long successMsg = 0;
        private CancellationTokenSource tokenSource = new();
        private long totalMsg = 0;
        private UdpClient udpClient;
        internal const int bufferLen = 40000;
        internal ConcurrentDictionary<Guid, FileRecvDicData> FileRecvDic = new();
        internal Stream? fs;
        internal ConcurrentDictionary<DataSliceInfo, DataSlice> slices = new();
        public bool IsPeerConnected { get; private set; } = false;
        public int Port { get; }

        #endregion Fields

        #region Delegates & Events

        public Func<BasicFileInfo, Task<(bool receive, string savePath)>>? OnInitFileTransfer;

        public event EventHandler? PeerConnected;

        public event EventHandler? PeerConnectionLost;

        public event EventHandler? PeersDataReceived;

        #endregion Delegates & Events

        #region Properties

        public Guid ID { get; }
        public IEnumerable<PeerInnerEP> LocalEP { get; }
        public string? Name { get; }
        public PeerEP? OuterEp { get; private set; }
        public ConcurrentDictionary<Guid, PeerInfo>? Peers { get; private set; }
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
            server.AddHandler<PeerDefaultHandlers>();
            server.ConfigureServices(services =>
            {
                services.AddSingleton(this);
            });
            server.AfterDataHandled += (s, e) => ResetPingCount();
            server.OnError += Server_OnError;
            _ = StartSendTask();
        }

        #region Workers

        private Task StartBroadCast()
            => Task.Run(async () =>
            {
                var peerInfo = new PeerInfo { Id = ID, InnerEP = LocalEP.ToList() };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<PeerInfo>
                {
                    Method = (int)CallMethods.Connect,
                    Data = peerInfo,
                });
                while (true)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    if (IsPeerConnected)
                    {
                        break;
                    }
                    msgs.Enqueue(new UdpMsg
                    {
                        Data = bytes,
                        Ep = serverEP
                    });
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
                    if (!IsPeerConnected)
                    {
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

        private Task StartQueuedTask<T>(MsgQueue<T> msgQueue, Func<T, Task> processor)
        {
            return Task.Run(async () =>
            {
                while (true)
                {
                    var msg = await msgQueue.DequeueAsync();
                    await processor(msg);
                }
            });
        }

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
                            Peers = JsonSerializer.Deserialize<ConcurrentDictionary<Guid, PeerInfo>>(re.Buffer)!;
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

        private Task StartSendTask()
        {
            return StartQueuedTask(msgs, async msg =>
            {
                await udpClient.SendAsync(msg.Data, msg.Data.Length, msg.Ep);
                if (msg.SendTask is not null)
                {
                    msg.SendTask.SetResult();
                }
            });
        }

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
                    await FileRecvDic[dataSlice.SessionId].IOTask;
                }
                catch (Exception)
                {
                }
                await fs!.DisposeAsync();
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

        internal async Task FileTransferRequested(BasicFileInfo data)
        {
            var (recv, savepath) = await (OnInitFileTransfer ??
                (async (info) => await Task.FromResult((true, info.Name)))).Invoke(data);
            var sessionId = data.SessionId;
            if (recv)
            {
                MsgQueue<DataSlice> queue = new();
                fs = File.Create(savepath);
                FileRecvDic[sessionId] = new FileRecvDicData
                {
                    SavePath = savepath,
                    Semaphore = new SemaphoreSlim(1),
                    Length = data.Length,
                    Watch = new Stopwatch(),
                    MsgQueue = queue,
                    IOTask = StartQueuedTask(queue, async fm =>
                    {
                        await fs.WriteAsync(fm.Slice, 0, fm.Len);
                        if (fm.Last)
                        {
                            throw new OperationCanceledException();
                        }
                    })
                };
            }
            await SendDataToPeerReliableAsync((int)CallMethods.FileHandShakeCallback, new FileTransferHandShakeResult
            {
                Accept = recv,
                SessionId = data.SessionId
            });
        }

        internal void OnFileHandshakeResult(UdpContext context)
        {
            var data = context.GetData<FileTransferHandShakeResult>().Data;
            successMsg = 0;
            totalMsg = 0;
            sendTimeOut = 1000;
            rtts.Clear();
            FileAcceptTasks[data.SessionId].SetResult(data.Accept);
        }

        internal async Task ProcessDataSliceAsync(DataSlice dataSlice, Func<Task> cleanUpAsync)
        {
            var semaphoreSlim = FileRecvDic[dataSlice.SessionId].Semaphore;
            async Task ProcessSliceAsync(DataSlice slice)
            {
                currentHead = slice.No;
                FileRecvDic[dataSlice.SessionId].MsgQueue.Enqueue(slice);
                if (slice.No % 1000 == 0)
                {
                    Console.WriteLine($"data transfered:{((slice.No + 1) * bufferLen / (double)FileRecvDic[dataSlice.SessionId].Length * 100),5}%");
                }
                if (slice.Last)
                {
                    await cleanUpAsync();
                }
            }
            await semaphoreSlim.WaitAsync();
            var sliceInfo = new DataSliceInfo { SessionId = dataSlice.SessionId };
            if (currentHead == dataSlice.No - 1)
            {
                await ProcessSliceAsync(dataSlice);
                sliceInfo.No = ++dataSlice.No;
                while (slices.TryRemove(sliceInfo,
                    out var slice))
                {
                    await ProcessSliceAsync(slice);
                    sliceInfo.No++;
                }
            }
            else
            {
                slices[new DataSliceInfo { No = dataSlice.No, SessionId = dataSlice.SessionId }] = dataSlice;
            }
            semaphoreSlim.Release();
        }

        public async Task SendFileAsync(string location, int concurrentLevel = 3)
        {
            using SemaphoreSlim semaphore = new(concurrentLevel);
            using var fs = File.OpenRead(location);
            var sessionId = Guid.NewGuid();
            FileAcceptTasks[sessionId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendDataToPeerReliableAsync((int)CallMethods.FileHandShake, new BasicFileInfo
            {
                Length = fs.Length,
                Name = Path.GetFileName(fs.Name),
                SessionId = sessionId
            });
            await FileAcceptTasks[sessionId].Task;
            var cancelSource = new CancellationTokenSource();
            var total = fs.Length / bufferLen;
            Console.WriteLine($"Slice count: {total}");
            for (long i = 0, j = 0; i < fs.Length; i += bufferLen, j++)
            {
                var buffer = new byte[bufferLen];
                await semaphore.WaitAsync();
                var len = await fs.ReadAsync(buffer, 0, bufferLen);
                var l = i >= fs.Length - bufferLen;
                cancelSource.Token.ThrowIfCancellationRequested();
                var j1 = j;
                var slice = new DataSlice
                {
                    No = j1,
                    Slice = buffer,
                    Len = len,
                    Last = l,
                    SessionId = sessionId
                };

                if (l)
                {
                    await SendDataToPeerReliableAsync((int)CallMethods.DataSlice, slice, 30, cancelSource.Token)
                            .ContinueWith(async re =>
                            {
                                var excr = await re;
                                semaphore.Release();
                                if (!excr)
                                {
                                    cancelSource.Cancel();
                                }
                            });
                }
                else
                {
                    _ = Task.Run(() =>
                    {
                        _ = SendDataToPeerReliableAsync((int)CallMethods.DataSlice, slice, 30, cancelSource.Token)
                            .ContinueWith(async re =>
                            {
                                var excr = await re;
                                semaphore.Release();
                                if (!excr)
                                {
                                    cancelSource.Cancel();
                                }
                            });
                    });
                }
            }
            Console.WriteLine($"Send complete. Lost = {(1 - (double)successMsg / totalMsg) * 100}%");
            Console.WriteLine($"Auto adjusted timeout: {sendTimeOut}ms");
        }

        #endregion File Transfer

        #region Handlers

        private void Server_OnError(object? sender, byte[] e)
        {
            var str = Encoding.Default.GetString(e);
            if (str == "Connected\n")
            {
                msgs.Enqueue(new UdpMsg
                {
                    Data = e,
                    Ep = peer!.OuterEP.ToIPEP()
                });
            }
        }

        internal void AckReturned(Guid reqId)
        {
            if (AckTasks.TryGetValue(reqId, out var src))
            {
                src.TrySetResult(true);
            }
        }

        internal async void PeerConnectedReceived()
        {
            if ((DateTime.UtcNow - lastConnectTime).TotalMilliseconds < 500)
            {
                return;
            }
            if (IsPeerConnected)
            {
                await SendDataToPeerAsync((int)CallMethods.Connected, "");
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
            if (ep != peer!.OuterEP && peer.InnerEP.Contains(new PeerInnerEP(ep)))
            {
                peer.OuterEP = ep;
            }
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

        public Task SendDataToPeerAsync<T>(T data) where T : class
        {
            return SendDataToPeerAsync((int)CallMethods.P2PDataTransfer, data);
        }

        public Task SendDataToPeerAsync<T>(int method, T data)
        {
            var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<T>
            {
                Method = method,
                Data = data,
            });
            msgs.Enqueue(new UdpMsg
            {
                Data = bytes,
                Ep = peer!.OuterEP.ToIPEP(),
                SendTask = t
            });
            return t.Task;
        }

        public virtual async Task<bool> SendDataToPeerReliableAsync<T>(T data, CancellationToken? token = null)
        {
            return await SendDataToPeerReliableAsync((int)CallMethods.P2PDataTransfer, data, 3, token);
        }

        public virtual async Task<bool> SendDataToPeerReliableAsync<T>(int method, T data, int retry = 10, CancellationToken? token = null)
        {
            Stopwatch? stopwatch = null;
            if (rtts.Count < avgNum)
            {
                stopwatch = new();
            }
            var reqId = Guid.NewGuid();
            AckTasks[reqId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<T>
            {
                Method = method,
                Data = data,
                ReqId = reqId
            });
            for (int i = 0; i < retry; i++)
            {
                token?.ThrowIfCancellationRequested();
                var ts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Interlocked.Increment(ref totalMsg);
                msgs.Enqueue(new UdpMsg
                {
                    Data = bytes,
                    Ep = peer!.OuterEP.ToIPEP(),
                    SendTask = ts
                });
                await ts.Task;
                if (rtts.Count < avgNum)
                {
                    if (stopwatch!.IsRunning)
                    {
                        stopwatch!.Restart();
                    }
                    else
                    {
                        stopwatch!.Start();
                    }
                }
                var t = await await Task.WhenAny(AckTasks[reqId].Task, ((Func<Task<bool>>)(async () =>
                {
                    await Task.Delay(sendTimeOut);
                    return false;
                }))());
                if (t)
                {
                    if (rtts.Count < avgNum)
                    {
                        rtts.Enqueue(stopwatch!.ElapsedMilliseconds);
                        if (rtts.Count == avgNum)
                        {
                            sendTimeOut = (int)rtts.OrderBy(i => i)
                                .Take((int)(0.98 * rtts.Count)).Max() * 2 + 1;
                        }
                    }
                    Interlocked.Increment(ref successMsg);
                    AckTasks.TryRemove(reqId, out var completionSource);
                    return true;
                }
            }

            AckTasks.TryRemove(reqId, out var taskCompletionSource);
            return false;
        }

        #endregion Send Data

        #region User Interface

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

        public void SetPeer(Guid id, bool inSubNet = false)
        {
            if (peer is not null)
            {
                return;
            }
            peer = Peers![id];
            // 自动切换至局域网内连接
            if (peer.OuterEP.IP == OuterEp!.IP || inSubNet)
            {
                foreach (var item in LocalEP)
                {
                    foreach (var item1 in peer.InnerEP)
                    {
                        if (item.IsInSameSubNet(item1))
                        {
                            peer.OuterEP = item1;
                        }
                    }
                }
            }
            Console.WriteLine($"Trying remote ep: {peer.OuterEP}");
        }

        public Task StartPeer()
        {
            var t = StartReceiveData();
            StartBroadCast();
            StartHolePunching();
            return t;
        }

        #endregion User Interface
    }
}