using Chronos.P2P.Client;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chronos.P2P.Server
{
    public record TypeData
    {
        public Type GenericType { get; init; }
        public ParameterInfo[] Parameters { get; init; }
        public MethodInfo Method { get; init; }
    }
    public class P2PServer
    {
        private const int listenPort = 5000;
        ConcurrentDictionary<Guid, PeerInfo> peers;
        Dictionary<int, TypeData> requestHandlers;
        Type attribute = typeof(HandlerAttribute);
        UdpClient listener;
        ServiceCollection services;
        ServiceProvider serviceProvider;
        public P2PServer() : this(new UdpClient(new IPEndPoint(IPAddress.Any, listenPort))) { }
        public P2PServer(UdpClient client)
        {
            services = new ServiceCollection();
            listener = client;
            peers = new ConcurrentDictionary<Guid, PeerInfo>();
            requestHandlers = new Dictionary<int, TypeData>();
        }
        public void ConfigureServices(Action<ServiceCollection> configureAction)
        {
            configureAction(services);
            serviceProvider = services.BuildServiceProvider();
        }
        public void AddDefaultServerHandler()
        {
            AddHandler<ServerHandlers>();
        }
        public void AddHandler<T>() where T:class
        {
            var type = typeof(T);
            var cstParams = type.GetConstructors()[0].GetParameters();
            var td = new TypeData { GenericType = type, Parameters = cstParams };

            
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
        void CallHandler(TypeData data, object param)
        {
            List<object> args = new List<object>();
            foreach (var item in data.Parameters)
            {
                args.Add(serviceProvider.GetRequiredService(item.ParameterType));
            }
            var handler = Activator.CreateInstance(data.GenericType, args);
            data.Method.Invoke(handler, new[] { param });
        }
        public async Task StartServerAsync()
        {
            
            
            try
            {
                while (true)
                {
                    Console.WriteLine("Waiting for broadcast");

                    var re = await listener.ReceiveAsync();
                    var dto = JsonSerializer.Deserialize<CallServerDto<object>>(re.Buffer);
                    var td = requestHandlers[dto.Method];
                    
                    CallHandler(td, new UdpContext
                    {
                        Dto = dto,
                        Peers = peers,
                        RemoteEndPoint = re.RemoteEndPoint,
                        UdpClient = listener
                    });
                    
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                listener.Close();
            }
        }

    }
}
