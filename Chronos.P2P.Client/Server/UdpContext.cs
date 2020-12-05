using Chronos.P2P.Client;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// udp请求上下文，是handler的参数。
    /// </summary>
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
        /// <summary>
        /// 获取上下文里的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        public CallServerDto<T> GetData<T>()
        {
            return JsonSerializer.Deserialize<CallServerDto<T>>(data);
        }
    }
}