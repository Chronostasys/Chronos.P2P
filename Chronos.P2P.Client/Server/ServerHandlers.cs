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
        [Handler((int)CallMethods.Connect)]
        public void HandleConnect(UdpContext context)
        {
            var peer = context.GetData<PeerInfo>().Data;
            var remote = context.RemoteEndPoint;
            var peers = context.Peers;
            peer.OuterEP = PeerEP.ParsePeerEPFromIPEP(remote);
            
            Console.WriteLine($"receive peer {peer.Id} from {peer.OuterEP.ToIPEP()}");

            if (peer.NeedData)
            {
                var sendbytes = JsonSerializer.SerializeToUtf8Bytes(peers);
                context.UdpClient.SendAsync(sendbytes, sendbytes.Length, remote);
            }
            
            peer.SetTimeOut(peers);
            if (peers.TryGetValue(peer.Id, out var prev))
            {
                prev.Dispose();
            }
            peers[peer.Id] = peer;
        }
    }
}
