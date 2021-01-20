using Chronos.P2P.Client;
using MessagePack;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// udp请求上下文，是handler的参数。
    /// </summary>
    public class UdpContext: IDisposable
    {
        public Memory<byte> Data { get; }
        public ConcurrentDictionary<Guid, PeerInfo> Peers { get; init; }

        public IPEndPoint RemoteEndPoint { get; init; }

        public Socket UdpClient { get; init; }
        public IMemoryOwner<byte>? MemOwner { get; private set; }
        /// <summary>
        /// for mock
        /// </summary>
        internal UdpContext() { }
        public UdpContext(Memory<byte> buffer, ConcurrentDictionary<Guid, PeerInfo> peers,
            IPEndPoint remoteEp, Socket client, IMemoryOwner<byte> owner)
        {
            Data = buffer;
            Peers = peers;
            RemoteEndPoint = remoteEp;
            UdpClient = client;
            MemOwner = owner;
        }

        /// <summary>
        /// 获取上下文里的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        public T? GetData<T>()
        {
            if (Data.Length == 0)
            {
                return default;
            }
            T t = MessagePackSerializer.Deserialize<T>(Data)!;
            Dispose();
            return t;
        }

        public virtual void Dispose()
        {
            MemOwner?.Dispose();
            MemOwner = null;
        }
    }
}