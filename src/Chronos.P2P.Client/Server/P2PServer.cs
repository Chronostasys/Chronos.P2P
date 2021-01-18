using Chronos.P2P.Client;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 一个简单udp服务器
    /// </summary>
    public class P2PServer : IRequestHandlerCollection, IDisposable
    {
        private const int avgNum = 100;
        private static readonly object rttLock = new();
        private static readonly ObjectPool<Stopwatch> timerPool = ObjectPool.Create<Stopwatch>();
        private readonly Type attribute = typeof(HandlerAttribute);
        private readonly Socket listener;
        private readonly ConcurrentDictionary<Guid, PeerInfo> peers;
        internal ServiceProvider? serviceProvider;
        internal readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> ackTasks = new();
        internal readonly AutoTimeoutData timeoutData = new();
        internal ConcurrentDictionary<PeerEP, (PeerEP, DateTime)> connectionDic = new();
        internal ConcurrentDictionary<Guid, DateTime> guidDic = new();
        internal MsgQueue<UdpMsg> msgs = new();
        internal Dictionary<int, TypeData> requestHandlers;
        internal ServiceCollection services;
        ILogger<P2PServer> _logger { get
            {
                if (logger is null)
                {
                    logger = serviceProvider!.GetRequiredService<ILogger<P2PServer>>();
                }
                return logger;
            } }
        ILogger<P2PServer>? logger = null;

        public event EventHandler? AfterDataHandled;

        public event EventHandler<byte[]>? OnError;

        public P2PServer(int port = 5000) : this(new Socket(SocketType.Dgram, ProtocolType.Udp))
        {
        }

        public P2PServer(Socket client)
        {
            MessagePackSerializer.DefaultOptions.WithSecurity(MessagePackSecurity.UntrustedData);
            services = new ServiceCollection();
            services.AddSingleton(this);
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();
            });
            listener = client;
            peers = new ConcurrentDictionary<Guid, PeerInfo>();
            requestHandlers = new Dictionary<int, TypeData>();
            _ = StartSendTask();
        }

        private static async Task<bool> waitAsync(int timeOut)
        {
            await Task.Delay(timeOut);
            return false;
        }

        internal static byte[] CreateUdpRequestBuffer(int callMethod, Guid reqId, byte[]? data = null)
        {
            if (data is null)
            {
                data = Array.Empty<byte>();
            }
            byte[] req = new byte[20 + data.Length];
            Span<byte> reqSpan = req;
            MemoryMarshal.Write(reqSpan[0..4], ref callMethod);
            MemoryMarshal.Write(reqSpan[4..20], ref reqId);
            Buffer.BlockCopy(data, 0, req, 20, data.Length);
            return req;
        }

        internal static byte[] CreateUdpRequestBuffer<T>(int callMethod, Guid reqId, T data)
        {
            return CreateUdpRequestBuffer(callMethod, reqId, data is byte[]? data as byte[] : MessagePackSerializer.Serialize(data));
        }
        Type handlerActionType = typeof(Action<UdpContext>);

        /// <summary>
        /// 调用请求对应的处理函数
        /// </summary>
        /// <param name="data">预存的类型信息</param>
        /// <param name="param">udp上下文</param>
        internal void CallHandler(TypeData data, UdpContext param)
        {
            var handler = GetInstance(data);
            var handlerAction = (Delegate.CreateDelegate(handlerActionType, handler, data.Method!) as Action<UdpContext>)!;
            Task.Run(() =>
            {
                handlerAction(param);
            });
        }

        /// <summary>
        /// 使用反射来运行时获取对应的Handler类
        /// </summary>
        /// <param name="data">注册handler时（<see cref="AddHandler{T}"/>）用反射获取的类型信息</param>
        /// <returns></returns>
        internal object GetInstance(TypeData data)
        {
            serviceProvider ??= services.BuildServiceProvider();
            List<object> args = new();
            foreach (var item in data.Parameters)
            {
                args.Add(serviceProvider!.GetRequiredService(item.ParameterType));
            }
            return Activator.CreateInstance(data.GenericType, args.ToArray())!;
        }

        internal async Task ProcessRequestAsync(IMemoryOwner<byte> bufferOwner, SocketReceiveFromResult result)
        {
            await Task.Yield();
            var mem = bufferOwner.Memory[0..result.ReceivedBytes];

            var method = mem.Slice(0, 4);
            var reqId = MemoryMarshal.Read<Guid>(mem[4..20].Span);
            var data = mem[20..];
            var mthd = BitConverter.ToInt32(method.ToArray());
            // 带有reqid的请求是reliable 的请求，需要在处理请求前返回ack消息
            if (reqId != Guid.Empty)
            {
                var bytes = CreateUdpRequestBuffer((int)CallMethods.Ack, Guid.Empty, reqId);
                msgs.Enqueue(new UdpMsg
                {
                    Data = bytes,
                    Ep = (result.RemoteEndPoint as IPEndPoint)!
                });
                if (guidDic.ContainsKey(reqId))
                {
                    // 如果guids里边包含此次的请求id，则说明之前已经处理过这个请求，但是我们返回的ack丢包了。
                    // 所以这里直接返回ack而不处理
                    return;
                }
                guidDic[reqId] = DateTime.UtcNow;
            }
            if (mthd != (int)CallMethods.Abort)
            {
                var td = requestHandlers[mthd];
                CallHandler(td, new UdpContext(data, peers,
                    (result.RemoteEndPoint as IPEndPoint)!, listener, bufferOwner));
            }
            AfterDataHandled?.Invoke(this, new());
        }

        internal virtual Task StartSendTask()
        {
            return Utils.StartQueuedTask(msgs, async msg =>
            {
                try
                {
                    await listener.SendToAsync(msg.Data, SocketFlags.None, msg.Ep);
                    if (msg.SendTask is not null)
                    {
                        msg.SendTask.SetResult(true);
                    }
                }
                catch (Exception)
                {
                    if (msg.SendTask is not null)
                    {
                        msg.SendTask.SetResult(false);
                    }
                }
            });
        }

        public static P2PServer BuildWithStartUp<T>(int port = 5000)
                                    where T : IStartUp, new()
        {
            return BuildWithStartUp<T>(new Socket(SocketType.Dgram, ProtocolType.Udp));
        }

        public static P2PServer BuildWithStartUp<T>(Socket client)
            where T : IStartUp, new()
        {
            var server = new P2PServer(client);
            var startUp = new T();
            startUp.Configure(server);
            server.ConfigureServices(startUp.ConfigureServices);
            return server;
        }

        public static async ValueTask<bool> SendDataReliableAsync<T>(int method, T data,
            IPEndPoint ep, ConcurrentDictionary<Guid, TaskCompletionSource<bool>> ackTasks,
            MsgQueue<UdpMsg> msgs, AutoTimeoutData timeoutData,
            int retry = 10, CancellationToken? token = null)
        {
            var timer = timerPool.Get();
            var reqId = Guid.NewGuid();
            ackTasks[reqId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dbytes = (data is byte[]) ? (data as byte[]) : MessagePackSerializer.Serialize(data);
            var bytes = CreateUdpRequestBuffer(method, reqId, dbytes);
            bool success = false;
            for (int i = 0; i < retry; i++)
            {
                token?.ThrowIfCancellationRequested();
                var ts = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                msgs.Enqueue(new UdpMsg
                {
                    Data = bytes,
                    Ep = ep,
                    SendTask = ts
                });
                var sent = await ts.Task;
                if (!sent)
                {
                    success = false;
                }
                else
                {
                    if (i == 0) timer.Start();
                    else timer.Restart();
                    success = await await Task.WhenAny(ackTasks[reqId].Task, waitAsync(timeoutData.SendTimeOut));
                }
                if (success)
                {
                    var sampleRtt = timer.Elapsed.TotalMilliseconds;
                    lock (rttLock)
                    {
                        if (timeoutData.EstimateRtt < 0)
                        {
                            timeoutData.EstimateRtt = (int)sampleRtt + 1;
                            timeoutData.DevRtt = (int)sampleRtt / 2 + 1;
                        }
                        else
                        {
                            timeoutData.DevRtt = (int)Math.Round(0.75 * timeoutData.DevRtt
                                + 0.25 * Math.Abs(timeoutData.EstimateRtt - sampleRtt));
                            timeoutData.EstimateRtt =
                                (int)Math.Round(0.875 * timeoutData.EstimateRtt + 0.125 * sampleRtt);
                        }
                    }
                    break;
                }
            }

            ackTasks.TryRemove(reqId, out _);
            timer.Reset();
            timerPool.Return(timer);
            return success;
        }

        /// <summary>
        /// 注册p2p服务器所需的默认服务
        /// </summary>
        public void AddDefaultServerHandler()
        {
            AddHandler<ServerHandlers>();
        }

        /// <summary>
        /// 注册处理类，保存类里处理方法的反射信息
        /// </summary>
        /// <typeparam name="T">Handler类</typeparam>
        public void AddHandler<T>() where T : class
        {
            var type = typeof(T);
            var ctor = type.GetConstructors()[0]!;
            var cstParams = ctor.GetParameters()!;
            var td = new TypeData(type, cstParams, null);

            var methods = type.GetMethods();
            foreach (var item in methods)
            {
                if (Attribute.GetCustomAttribute(item, attribute) is HandlerAttribute attr)
                {
                    requestHandlers[attr.Method] = td with { Method = item };
                }
            }
        }

        /// <summary>
        /// 类似asp.net core的设计，用于依赖注入
        /// 默认会注入自身为单例服务
        /// </summary>
        /// <param name="configureAction">用于依赖注入的方法</param>
        public void ConfigureServices(Action<ServiceCollection> configureAction)
        {
            configureAction(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            listener?.Dispose();
        }

        public ValueTask<bool> SendDataReliableAsync<T>(int method, T data,
            IPEndPoint ep, int retry = 10, CancellationToken? token = null)
        {
            return SendDataReliableAsync(method, data, ep, ackTasks, msgs, timeoutData, retry, token);
        }

        ArrayPool<byte> receivePool = ArrayPool<byte>.Shared;
        internal static IPEndPoint allEp = new IPEndPoint(IPAddress.Any, 0);
        /// <summary>
        /// 启动消息接收的循环
        /// </summary>
        /// <returns></returns>
        public async Task StartServerAsync()
        {
            if (serviceProvider is null)
            {
                ConfigureServices(s => { });
            }
            var t = Task.Run((Func<Task>)(async () =>
            {
                // 启动一个线程，每10秒自动清除掉已经结束超过10秒的reliable请求id
                while (true)
                {
                    await Task.Delay(10000);
                    foreach (var item in guidDic)
                    {
                        if ((DateTime.UtcNow - item.Value).TotalSeconds > 10)
                        {
                            guidDic.TryRemove(item);
                        }
                    }
                    foreach (var item in connectionDic)
                    {
                        if ((DateTime.UtcNow - item.Value.Item2).TotalSeconds > 10)
                        {
                            connectionDic.TryRemove(item);
                        }
                    }
                }
            }));
            while (true)
            {
                byte[] receiveMem = receivePool.Rent(Peer.bufferLen)!;
                
                var re = await listener.ReceiveFromAsync(receiveMem, SocketFlags.None, allEp);
                var owner = new ReceiveBufferOwner(receiveMem);
                try
                {
                    _ = ProcessRequestAsync(owner, re);
                }
                catch (Exception)
                {
                    //OnError?.Invoke(this, re.Buffer);
                }
            }
        }
    }
    public class ReceiveBufferOwner : IMemoryOwner<byte>
    {
        public ReceiveBufferOwner(byte[] buffer)
        {
            Buffer = buffer;
            Memory = buffer;
        }
        public byte[] Buffer { get; }
        public Memory<byte> Memory { get; }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }
}