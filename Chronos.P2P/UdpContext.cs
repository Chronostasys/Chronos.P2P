using Chronos.P2P.Client;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Chronos.P2P.Server
{
    public class UdpContext
    {
        public UdpClient UdpClient { get; init; }
        public ConcurrentDictionary <Guid, PeerInfo> Peers { get; init; }
        public IPEndPoint RemoteEndPoint { get; init; }
        public CallServerDto<object> Dto { get; init; }


    }
}
