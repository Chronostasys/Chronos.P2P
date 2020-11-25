using Chronos.P2P.Client;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
[assembly: InternalsVisibleTo("Chronos.P2P.Test")]
namespace Chronos.P2P.Server
{
    public class P2PServer:IDisposable
    {
        private const int listenPort = 5000;
        ConcurrentDictionary<Guid, PeerInfo> peers;
        internal Dictionary<int, TypeData> requestHandlers;
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
            services.AddSingleton(this);
            serviceProvider = services.BuildServiceProvider();
        }
        public void AddDefaultServerHandler()
        {
            AddHandler<ServerHandlers>();
        }
        public void AddHandler<T>() where T:class
        {
            var type = typeof(T);
            var ctor = type.GetConstructors()[0];
            var cstParams = ctor.GetParameters();
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
        
        internal void CallHandler(TypeData data, UdpContext param)
        {
            var handler = GetInstance(data);
            data.Method.Invoke(handler, new[] { param });
        }
        internal object GetInstance(TypeData data)
        {
            List<object> args = new List<object>();
            foreach (var item in data.Parameters)
            {
                args.Add(serviceProvider.GetRequiredService(item.ParameterType));
            }
            return Activator.CreateInstance(data.GenericType, args.ToArray());
        }
        public async Task StartServerAsync()
        {
            if (serviceProvider is null)
            {
                ConfigureServices(s => { });
            }
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

        public void Dispose()
        {
            listener.Dispose();
        }
    }
}
