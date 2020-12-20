using Chronos.P2P.Client;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
        private const int avgNum = 1000;
        private Type attribute = typeof(HandlerAttribute);
        private UdpClient listener;
        private ConcurrentDictionary<Guid, PeerInfo> peers;
        private ServiceProvider? serviceProvider;
        internal readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> ackTasks = new();
        internal readonly AutoTimeoutData timeoutData = new();
        internal ConcurrentDictionary<PeerEP, (PeerEP, DateTime)> connectionDic = new();
        internal ConcurrentDictionary<Guid, DateTime> guidDic = new();
        internal MsgQueue<UdpMsg> msgs = new();
        internal Dictionary<int, TypeData> requestHandlers;
        internal ServiceCollection services;

        public event EventHandler? AfterDataHandled;

        public event EventHandler<byte[]>? OnError;

        public P2PServer(int port = 5000) : this(new UdpClient(new IPEndPoint(IPAddress.Any, port)))
        {
        }

        public P2PServer(UdpClient client)
        {
            services = new ServiceCollection();
            services.AddSingleton(this);
            listener = client;
            peers = new ConcurrentDictionary<Guid, PeerInfo>();
            requestHandlers = new Dictionary<int, TypeData>();
            _ = StartSendTask();
        }

        internal virtual Task StartSendTask()
        {
            return Utils.StartQueuedTask(msgs, async msg =>
            {
                await listener.SendAsync(msg.Data, msg.Data.Length, msg.Ep);
                if (msg.SendTask is not null)
                {
                    msg.SendTask.SetResult();
                }
            });
        }

        /// <summary>
        /// 调用请求对应的处理函数
        /// </summary>
        /// <param name="data">预存的类型信息</param>
        /// <param name="param">udp上下文</param>
        internal void CallHandler(TypeData data, UdpContext param)
        {
            var handler = GetInstance(data);
            Task.Run(() =>
            {
                data.Method!.Invoke(handler, new[] { param });
            });
        }

        /// <summary>
        /// 使用反射来运行时获取对应的Handler类
        /// </summary>
        /// <param name="data">注册handler时（<see cref="AddHandler{T}"/>）用反射获取的类型信息</param>
        /// <returns></returns>
        internal object GetInstance(TypeData data)
        {
            serviceProvider = serviceProvider ?? services.BuildServiceProvider();
            List<object> args = new List<object>();
            foreach (var item in data.Parameters)
            {
                args.Add(serviceProvider!.GetRequiredService(item.ParameterType));
            }
            return Activator.CreateInstance(data.GenericType, args.ToArray())!;
        }

        internal static byte[] CreateUdpRequestBuffer(int callMethod, Guid reqId, byte[]? data = null)
        {
            var mthd = BitConverter.GetBytes(callMethod);
            var id = reqId.ToByteArray();
            if (data is null)
            {
                data = new byte[0];
            }
            var req = new byte[mthd.Length+id.Length+data.Length];
            Buffer.BlockCopy(mthd, 0, req, 0, mthd.Length);
            Buffer.BlockCopy(id, 0, req, 4, id.Length);
            Buffer.BlockCopy(data, 0, req, 20, data.Length);
            return req;
        }
        internal static byte[] CreateUdpRequestBuffer<T>(int callMethod, Guid reqId, T data)
        {
            return CreateUdpRequestBuffer(callMethod, reqId, data is byte[]? data as byte[]: JsonSerializer.SerializeToUtf8Bytes(data));
        }

        internal async Task ProcessRequestAsync(UdpReceiveResult re)
        {
            await Task.Yield();
            var mem = new Memory<byte>(re.Buffer);

            var method = mem.Slice(0, 4);
            var reqId = new Guid(mem.Slice(4, 16).ToArray());
            var data = mem.Slice(20);
            var td = requestHandlers[BitConverter.ToInt32(method.ToArray())];
            // 带有reqid的请求是reliable 的请求，需要在处理请求前返回ack消息
            if (reqId != Guid.Empty)
            {
                var bytes = CreateUdpRequestBuffer((int)CallMethods.Ack, Guid.Empty, reqId);
                msgs.Enqueue(new UdpMsg
                {
                    Data = bytes,
                    Ep = re.RemoteEndPoint
                });
                if (guidDic.ContainsKey(reqId))
                {
                    // 如果guids里边包含此次的请求id，则说明之前已经处理过这个请求，但是我们返回的ack丢包了。
                    // 所以这里直接返回ack而不处理
                    return;
                }
                guidDic[reqId] = DateTime.UtcNow;
            }
            CallHandler(td, new UdpContext(data.ToArray(), peers, re.RemoteEndPoint, listener));
            AfterDataHandled?.Invoke(this, new());

        }

        public static P2PServer BuildWithStartUp<T>(int port = 5000)
                                    where T : IStartUp, new()
        {
            return BuildWithStartUp<T>(new UdpClient(new IPEndPoint(IPAddress.Any, port)));
        }

        public static P2PServer BuildWithStartUp<T>(UdpClient client)
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
            Stopwatch? stopwatch = null;
            if (timeoutData.Rtts.Count < avgNum)
            {
                stopwatch = new();
            }
            var reqId = Guid.NewGuid();
            ackTasks[reqId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dbytes = (data is byte[])?(data as byte[]): JsonSerializer.SerializeToUtf8Bytes(data);
            var bytes = CreateUdpRequestBuffer(method, reqId, dbytes);
            for (int i = 0; i < retry; i++)
            {
                token?.ThrowIfCancellationRequested();
                var ts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                msgs.Enqueue(new UdpMsg
                {
                    Data = bytes,
                    Ep = ep,
                    SendTask = ts
                });
                await ts.Task;
                if (timeoutData.Rtts.Count < avgNum)
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
                var t = await await Task.WhenAny(ackTasks[reqId].Task, ((Func<Task<bool>>)(async () =>
                {
                    await Task.Delay(timeoutData.SendTimeOut);
                    return false;
                }))());
                if (t)
                {
                    if (timeoutData.Rtts.Count < avgNum)
                    {
                        timeoutData.Rtts.Enqueue(stopwatch!.ElapsedMilliseconds);
                        if (timeoutData.Rtts.Count == avgNum)
                        {
                            timeoutData.SendTimeOut = (int)timeoutData.Rtts.OrderBy(i => i)
                                .Take((int)(0.98 * timeoutData.Rtts.Count)).Max() * 2 + 1;
                        }
                    }
                    ackTasks.TryRemove(reqId, out var completionSource);
                    return true;
                }
            }

            ackTasks.TryRemove(reqId, out var taskCompletionSource);
            return false;
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
                var attr = Attribute.GetCustomAttribute(item, attribute) as HandlerAttribute;
                if (attr != null)
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
            listener?.Dispose();
        }

        public ValueTask<bool> SendDataReliableAsync<T>(int method, T data, 
            IPEndPoint ep, int retry = 10, CancellationToken? token = null)
        {
            return SendDataReliableAsync(method, data, ep, ackTasks, msgs, timeoutData, retry, token);
        }

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
                var re = await listener.ReceiveAsync();

                try
                {
                    _ = ProcessRequestAsync(re);
                }
                catch (Exception)
                {
                    OnError?.Invoke(this, re.Buffer);
                }
            }
        }
    }
}