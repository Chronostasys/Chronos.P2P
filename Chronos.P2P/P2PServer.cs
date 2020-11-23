using Chronos.P2P.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chronos.P2P.Server
{
    public class P2PServer
    {
        private const int listenPort = 11000;
        ConcurrentDictionary<Guid, PeerInfo> peers;
        Dictionary<ServerMethods, Action<object>> requestHandlers;
        Type attribute = typeof(HandlerAttribute);
        public P2PServer()
        {
            peers = new ConcurrentDictionary<Guid, PeerInfo>();
            requestHandlers = new Dictionary<ServerMethods, Action<object>>();
            AddHandler<ServerHandlers>();
        }
        public void AddHandler<T>() where T:new()
        {
            var handler = new T();
            var methods = handler.GetType().GetMethods();
            foreach (var item in methods)
            {
                var attr = Attribute.GetCustomAttribute(item, attribute) as HandlerAttribute;
                if (attr != null)
                {
                    requestHandlers.Add(attr.Method, (c) => item.Invoke(handler,
                        new[]
                        {
                            c
                        }));
                }
            }

        }

        public async Task StartServerAsync()
        {
            UdpClient listener = new UdpClient(listenPort);
            
            
            try
            {
                while (true)
                {
                    Console.WriteLine("Waiting for broadcast");

                    var re = await listener.ReceiveAsync();
                    var dto = JsonSerializer.Deserialize<CallServerDto<object>>(re.Buffer);
                    requestHandlers[dto.Method](new UdpContext
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
