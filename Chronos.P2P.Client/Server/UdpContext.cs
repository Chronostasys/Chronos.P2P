using Chronos.P2P.Client;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Chronos.P2P.Server
{
    public class UdpContext
    {
        public byte[] data { get; }
        public ConcurrentDictionary<Guid, PeerInfo> Peers { get; init; }

        public IPEndPoint RemoteEndPoint { get; init; }

        public UdpClient UdpClient { get; init; }

        public UdpContext(byte[] buffer)
        {
            data = buffer;
        }

        public CallServerDto<T> GetData<T>() where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<CallServerDto<T>>(data);
            }
            catch (Exception)
            {
                var t = Encoding.Default.GetString(data);
                throw;
            }
        }
    }
}