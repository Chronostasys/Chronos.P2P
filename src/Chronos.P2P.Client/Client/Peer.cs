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
using Microsoft.Extensions.Logging;

namespace Chronos.P2P.Client
{
    public class Progress
    {
        public string WorkProgress { get; set; } = "Startup";
        public double Percent { get; set; }
        public void Report(double percent, string? progress = null)
        {
            Percent = percent;
            if (progress is not null)
            {
                WorkProgress = progress;
            }
        }
    }
    public class FixedLengthBufferPool : ObjectPool<byte[]>
    {
        int len;
        ConcurrentQueue<byte[]> queue = new();
        public FixedLengthBufferPool(int length)
            => len = length;
        public override byte[] Get()
        {
            if (queue.TryDequeue(out var buffer))
                return buffer;
            else
                return new byte[len];
        }

        public override void Return(byte[] obj)
        {
            queue.Enqueue(obj);
        }
    }
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
        private readonly Socket udpClient;
        private long currentHead = -1;
        private volatile bool isInSameSubNet = false;
        private DateTime lastConnectDataSentTime;
        private DateTime lastPunchDataSentTime;
        private int pingCount = 10;
        private ILogger<Peer> _logger;
        internal static int bufferLen = 65535;
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
            udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpClient.Bind(new IPEndPoint(IPAddress.Any, port));
            udpClient.DontFragment = true;// disable ip fragment for better transfer reliability
            this.Port = port;
            LocalEP = GetEps();
            server = new P2PServer(udpClient);
            server.AddDefaultServerHandler();
            server.AddHandler<PeerDefaultHandlers>();
            server.services.AddSingleton(this);
            server.AfterDataHandled += (s, e) => ResetPingCount();
            server.OnError += Server_OnError;
            using var p = server.services.BuildServiceProvider();
            _logger = p.GetRequiredService<ILogger<Peer>>();
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
                        _logger.LogWarning($"Connection lost!");
                        PeerConnectionLost?.Invoke(this, new());
                        peer = null;
                    }
                }
            });

        private Task StartReceiveData()
            => Task.Run(async () =>
            {
                byte[] dgram = ArrayPool<byte>.Shared.Rent(bufferLen);
                var memOwner = new ReceiveBufferOwner(dgram);
                while (true)
                {
                    var re = await udpClient.ReceiveFromAsync(dgram, SocketFlags.None, P2PServer.allEp);
                    var sege = new ArraySegment<byte>(dgram, 0, re.ReceivedBytes);
                    if (peer is null)
                    {
                        try
                        {
                            Peers = MessagePackSerializer
                                .Deserialize<ConcurrentDictionary<Guid, PeerInfo>>(sege.Array);
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
                            await server.ProcessRequestAsync(memOwner, re);
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
                        _logger.LogInformation($"trying new ep {peer.OuterEP}");
                    }
                    if (peer is not null)
                    {
                        if (IsPeerConnected)
                        {
                            break;
                        }
                        if (tokenSource.IsCancellationRequested)
                        {
                            _logger.LogInformation($"Connected data sent to peer {peer.OuterEP.ToIPEP()}");
                            await SendDataToPeerAsync((int)CallMethods.Connected, "");
                            if (IsPeerConnected)
                            {
                                break;
                            }
                        }
                        else
                        {
                            _logger.LogInformation($"Punching data sent to peer {peer.OuterEP.ToIPEP()}");
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

        internal async ValueTask FileDataReceived(DataSlice dataSlice)
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

            async ValueTask CleanUpAsync()
            {
                slices.Clear();
                currentHead = -1;
                _logger.LogInformation("Waiting for io to complete...");
                //try
                //{
                //    await FileRecvDic[dataSlice.SessionId].IOTask;
                //}
                //catch (Exception)
                //{
                //}
                await FileRecvDic[dataSlice.SessionId].FS.DisposeAsync();
                FileRecvDic.TryRemove(dataSlice.SessionId, out var val);
                _logger.LogInformation("transfer done!");
                val.Watch.Stop();
                _logger.LogInformation($"Time eplased: {val.Watch.Elapsed.TotalSeconds}s");
                _logger.LogInformation($"Speed: {val.Length / val.Watch.Elapsed.TotalSeconds / 1024 / 1024}MB/s");
                fs = null;
                val.Semaphore.Dispose();
            }
            await ProcessDataSliceAsync(dataSlice, CleanUpAsync);
        }

        internal void OnStreamHandshakeResult(UdpContext context)
        {
            var data = context.GetData<FileTransferHandShakeResult>();
            FileAcceptTasks[data.SessionId].SetResult(data.Accept);
        }

        internal async ValueTask ProcessDataSliceAsync(DataSlice dataSlice, Func<ValueTask> cleanUpAsync)
        {
            var recvData = FileRecvDic[dataSlice.SessionId];
            var semaphoreSlim = recvData.Semaphore;
            async ValueTask ProcessSliceAsync(DataSlice slice)
            {
                currentHead = slice.No;
                if (currentHead%nSlices is 0)
                {
                    if (currentHead is not 0)
                    {
                        await recvData.LastWriteTask;
                        var temp = recvData.FSWriteBuffer;
                        recvData.FSWriteBuffer = recvData.WriteBuffer;
                        recvData.WriteBuffer = recvData.FSWriteBuffer;
                        recvData.LastWriteTask = recvData.FS.WriteAsync(recvData.FSWriteBuffer);
                    }
                }
                slice.Slice.CopyTo(
                    recvData.WriteBuffer[((int)(currentHead % nSlices) * bufferLen)..
                        ((int)(currentHead % nSlices) * bufferLen + bufferLen)]);
                //await recvData.FS.WriteAsync(slice.Slice.Slice(0, slice.Len));
                slice.Context.Dispose();
                if (slice.No % 
                    ((recvData.Total/100)==0?1: (recvData.Total / 100))
                    == 0)
                {
                    _logger.LogInformation($"data transfered:{((slice.No + 1) * bufferLen / (double)recvData.Length * 100).ToString("0.00"),5}%");
                }
                if (slice.Last)
                {
                    await recvData.LastWriteTask;
                    await recvData.FS.WriteAsync(recvData.WriteBuffer[0..((int)(currentHead % nSlices) * bufferLen + slice.Len)]);
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
            try
            {
                semaphoreSlim.Release();
            }
            catch (Exception)
            {
            }
        }
        int nSlices = 10485760 / bufferLen;
        internal async ValueTask StreamTransferRequested(BasicFileInfo data)
        {
            var (recv, savepath) = await (OnInitFileTransfer ??
                (async (info) => await Task.FromResult((true, info.Name)))).Invoke(data);
            var sessionId = data.SessionId;
            if (recv)
            {
                if (data.Length > 0)
                {
                    var fileStream = File.Create(savepath, nSlices * bufferLen);
                    //fileStream.SetLength(data.Length);
                    FileRecvDic[sessionId] = new FileRecvDicData
                    {
                        SavePath = savepath,
                        Semaphore = new SemaphoreSlim(1),
                        Length = data.Length,
                        Watch = new Stopwatch(),
                        FS = fileStream,
                        Total = (data.Length/bufferLen==0)?1: data.Length / bufferLen,
                        WriteBuffer = new byte[(nSlices * bufferLen)],
                        FSWriteBuffer = new byte[(nSlices * bufferLen)],
                    };
                }
            }
            await SendDataToPeerReliableAsync((int)CallMethods.StreamHandShakeCallback, new FileTransferHandShakeResult
            {
                Accept = recv,
                SessionId = data.SessionId
            });
        }
        public static void DecorateSlice(bool last, int offset, int len, long no, Guid sessionId, byte[] slice)
        {
            Span<byte> dataSpan = slice;
            dataSpan = dataSpan[offset..];
            MemoryMarshal.Write(dataSpan, ref last);
            MemoryMarshal.Write(dataSpan[1..], ref len);
            MemoryMarshal.Write(dataSpan[5..], ref no);
            MemoryMarshal.Write(dataSpan[13..], ref sessionId);
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

        ObjectPool<byte[]>? sendPool;

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
        public async ValueTask SendFileAsync(string location, int concurrentLevel = 3,
            Action<Progress>? progressInvoker = null)
        {
            sendPool ??= new FixedLengthBufferPool(49 + bufferLen);
            CancellationTokenSource progressCancel = new();
            var progress = new Progress();
            if (progressInvoker is not null)
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        progressCancel.Token.ThrowIfCancellationRequested();
                        progressInvoker(progress);
                        await Task.Delay(100, progressCancel.Token);
                    }
                }, progressCancel.Token);
            }
            try
            {
                using SemaphoreSlim semaphore = new(concurrentLevel);
                using var fs = File.OpenRead(location);
                var sessionId = Guid.NewGuid();
                FileAcceptTasks[sessionId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                progress.Report(0f, "waiting for remote agreement...");
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
                progress.Report(0f, "remote accept");
                var cancelSource = new CancellationTokenSource();
                var total = fs.Length / bufferLen;
                _logger.LogInformation($"Slice count: {total}");
                Memory<byte> fileReadBuffer = new byte[(nSlices * bufferLen)];
                int readLen = 0;
                progress.Report(0f, "start file sending");
                for (long i = 0, j = 0; i < fs.Length; i += bufferLen, j++)
                {
                    int n = (int)(j % nSlices);
                    if (n == 0)
                    {
                        var rt = fs.ReadAsync(fileReadBuffer);
                        await semaphore.WaitAsync();
                        readLen = await rt;
                    }
                    else
                    {
                        await semaphore.WaitAsync();
                    }
                    var buffer = sendPool.Get();
                    Memory<byte> bm = buffer;
                    fileReadBuffer[(n * bufferLen)..((n + 1) * bufferLen)].CopyTo(bm[49..]);

                    var l = i >= fs.Length - bufferLen;
                    var len = l ? (readLen - n * bufferLen) : bufferLen;
                    cancelSource.Token.ThrowIfCancellationRequested();
                    var j1 = j;
                    DecorateSlice(l, 20, len, j1, sessionId, buffer);
                    var reqId = Guid.NewGuid();
                    P2PServer.DecorateRequestBuffer((int)CallMethods.DataSlice, reqId, buffer);
                    progress.Report(((((double)i) / ((double)fs.Length)) * 100.0));
                    void sendCleanUp(bool excr)
                    {
                        semaphore.Release();
                        if (!excr)
                        {
                            cancelSource!.Cancel();
                        }
                        sendPool.Return(buffer);
                    }
                    if (l)
                    {
                        var excr = await server.SendDirectDataReliableAsync(reqId, (int)CallMethods.DataSlice,
                            buffer, peer!.OuterEP.ToIPEP(),
                            30, cancelSource.Token);
                        sendCleanUp(excr);
                    }
                    else
                    {
                        _ = Task.Run(async () =>
                        {
                            var excr = await server.SendDirectDataReliableAsync(reqId, (int)CallMethods.DataSlice,
                                buffer, peer!.OuterEP.ToIPEP(),
                                30, cancelSource.Token);
                            sendCleanUp(excr);
                        });
                    }
                }
                _logger.LogInformation($"Auto adjusted timeout: {server.timeoutData.SendTimeOut}ms");
                if (semaphore.CurrentCount == concurrentLevel)
                {
                    semaphore.Release();
                }
            }
            finally
            {
                progressCancel.Cancel();
            }
        }

        public async ValueTask SendLiveStreamAsync(MsgQueue<(byte[], int)> channel, string name, int callMethod, CancellationToken token = default)
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
            var acc = await FileAcceptTasks[sessionId].Task;
            if (!acc)
            {
                throw new OperationCanceledException("Remote refused!");
            }
            await foreach (var (buffer, len) in channel)
            {
                token.ThrowIfCancellationRequested();
                Memory<byte> mem = buffer;
                for (int i = 0; i < len / bufferLen + 1; i++)
                {
                    var left = len - i * bufferLen;
                    var sendLen = ((left < bufferLen) ? left : bufferLen);
                    _ = SendDataReliableAsync(callMethod, SliceToBytes(false, sendLen,
                        no++, sessionId, mem[(i * bufferLen)..(i * bufferLen + sendLen)]), peer!.OuterEP.ToIPEP()).AsTask();
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

        internal async ValueTask OnConnectionRequested(PeerInfo requester)
        {
            var re = OnPeerInvited?.Invoke(requester);
            if (!re.HasValue || re.Value)
            {
                _logger.LogInformation("accept!");
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
            _logger.LogInformation("Peer connected");
            if (!IsPeerConnected)
            {
                IsPeerConnected = true;
                try
                {
                    await TestMTUAsync();
                }
                catch (Exception e)
                {

                    _logger.LogError($"{e}");
                }
                _ = Task.Run(() =>
                {
                    PeerConnected?.Invoke(this, new EventArgs());
                });
            }
        }
        internal async ValueTask TestMTUAsync()
        {
            _logger.LogInformation("Start testing MTU value");
            Memory<byte> data = new byte[65535];
            int min = 0;
            int max = 65535;
            int len = max / 2;
            while (true)
            {
                var b = data[0..len].ToArray();
                var canRecv = await SendDataToPeerReliableAsync((int)CallMethods.Abort,
                    b);
                if (!canRecv)
                    max = len;
                else
                    min = len;
                len = (min + max) / 2;
                if (min == len)
                {
                    bufferLen = len - 30;
                    break;
                }
            }
            nSlices = 10485760 / bufferLen;
            _logger.LogInformation($"MTU value: {bufferLen}");
        }

        internal async void PunchDataReceived(IPEndPoint rep)
        {
            var ep = PeerEP.ParsePeerEPFromIPEP(rep);
            lock (epKey)
            {
                peer!.OuterEP = ep;
                epConfirmed = true;
                _logger.LogInformation($"Peer punch data received, set remote ep to {peer.OuterEP}");
            }
            if (tokenSource.IsCancellationRequested
                && (DateTime.UtcNow - lastPunchDataSentTime).TotalMilliseconds > 500)
            {
                lastPunchDataSentTime = DateTime.UtcNow;
                await SendDataToPeerAsync((int)CallMethods.PunchHole, "");
                return;
            }
            tokenSource.Cancel();
            _logger.LogInformation("Connected!");
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

        public Task<bool> SendDataAsync<T>(int method, T data, IPEndPoint ep)
        {
            var t = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

        public Task<bool> SendDataToPeerAsync<T>(T data) where T : class
        {
            return SendDataToPeerAsync((int)CallMethods.P2PDataTransfer, data);
        }

        public Task<bool> SendDataToPeerAsync<T>(int method, T data)
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
        public static Peer Build(int port, IPEndPoint serverEP, string? name = null)
        {
            return BuildWithStartUp<EmptyStartup>(port, serverEP, name);
        }

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
    public class EmptyStartup : IStartUp
    {
        public void Configure(IRequestHandlerCollection handlers)
        {
        }

        public void ConfigureServices(IServiceCollection services)
        {
        }
    }
}