using Chronos.P2P.Client;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using MessagePack;

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

        public UdpContext(byte[] buffer, ConcurrentDictionary<Guid, PeerInfo> peers, IPEndPoint remoteEp, UdpClient client)
        {
            data = buffer;
            Peers = peers;
            RemoteEndPoint = remoteEp;
            UdpClient = client;
        }

        /// <summary>
        /// 获取上下文里的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        public T? GetData<T>()
        {
            if (data.Length == 0)
            {
                return default;
            }
            return MessagePackSerializer.Deserialize<T>(data)!;
        }
    }
}