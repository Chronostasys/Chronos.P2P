using Chronos.P2P.Client;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Chronos.P2P.Server
{
    public class ServerHandlers
    {
        [Handler(ServerMethods.Connect)]
        public void HandleConnect(UdpContext context)
        {
            var peer = context.Dto.GetData<PeerInfo>();
            var remote = context.RemoteEndPoint;
            var peers = context.Peers;
            peer.OuterEP = PeerEP.ParsePeerEPFromIPEP(remote);
            var sendbytes = JsonSerializer.SerializeToUtf8Bytes(peers);
            Console.WriteLine($"receive peer {peer.Id} from {peer.OuterEP.ToIPEP()}");
            context.UdpClient.SendAsync(sendbytes, sendbytes.Length, remote);
            peer.SetTimeOut(peers);
            if (peers.TryGetValue(peer.Id, out var prev))
            {
                prev.Dispose();
            }
            peers[peer.Id] = peer;
        }
    }
}
