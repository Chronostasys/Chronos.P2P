using Chronos.P2P.Client;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 一个简单udp服务器
    /// </summary>
    public class P2PServer : IDisposable
    {
        private Type attribute = typeof(HandlerAttribute);
        internal ConcurrentDictionary<Guid, DateTime> guidDic = new();
        private UdpClient listener;
        private ConcurrentDictionary<Guid, PeerInfo> peers;
        private ServiceProvider? serviceProvider;
        private ServiceCollection services;
        internal Dictionary<int, TypeData> requestHandlers;

        public event EventHandler? AfterDataHandled;

        public event EventHandler<byte[]>? OnError;

        public P2PServer(int port = 5000) : this(new UdpClient(new IPEndPoint(IPAddress.Any, port)))
        {
        }

        public P2PServer(UdpClient client)
        {
            services = new ServiceCollection();
            listener = client;
            peers = new ConcurrentDictionary<Guid, PeerInfo>();
            requestHandlers = new Dictionary<int, TypeData>();
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
            List<object> args = new List<object>();
            foreach (var item in data.Parameters)
            {
                args.Add(serviceProvider!.GetRequiredService(item.ParameterType));
            }
            return Activator.CreateInstance(data.GenericType, args.ToArray())!;
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
            services.AddSingleton(this);
            serviceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            listener?.Dispose();
        }

        internal async Task ProcessRequestAsync(UdpReceiveResult re)
        {
            var dto = JsonSerializer.Deserialize<UdpRequest>(re.Buffer)!;
            var td = requestHandlers[dto.Method];
            // 带有reqid的请求是reliable 的请求，需要在处理请求前返回ack消息
            if (dto.ReqId != Guid.Empty)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<Guid>
                {
                    Method = (int)CallMethods.Ack,
                    Data = dto.ReqId
                });
                await listener.SendAsync(bytes, bytes.Length, re.RemoteEndPoint);
                if (guidDic.ContainsKey(dto.ReqId))
                {
                    // 如果guids里边包含此次的请求id，则说明之前已经处理过这个请求，但是我们返回的ack丢包了。
                    // 所以这里直接返回ack而不处理
                    return;
                }
                guidDic[dto.ReqId] = DateTime.UtcNow;
            }
            CallHandler(td, new UdpContext(re.Buffer, peers, re.RemoteEndPoint, listener));
            AfterDataHandled?.Invoke(this, new());
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
                }
            }));
            while (true)
            {
                var re = await listener.ReceiveAsync();

                try
                {
                    await ProcessRequestAsync(re);
                }
                catch (Exception)
                {
                    OnError?.Invoke(this, re.Buffer);
                }
            }
        }
    }
}