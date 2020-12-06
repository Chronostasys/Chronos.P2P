﻿using Chronos.P2P.Client;
using System;
using System.Text.Json;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 默认的p2pserver请求处理类
    /// </summary>
    public class ServerHandlers
    {
        [Handler((int)CallMethods.Connect)]
        public void HandleConnect(UdpContext context)
        {
            var peer = context.GetData<PeerInfo>().Data;
            
            var remote = context.RemoteEndPoint;
            var peers = context.Peers;
            foreach (var item in peers)
            {
                if ((DateTime.UtcNow-item.Value.CreateTime).TotalSeconds>10)
                {
                    peers.TryRemove(item);
                }
            }
            peers[peer.Id] = peer;
            peer.OuterEP = PeerEP.ParsePeerEPFromIPEP(remote);

            Console.WriteLine($"receive peer {peer.Id} from {peer.OuterEP.ToIPEP()}");

            var sendbytes = JsonSerializer.SerializeToUtf8Bytes(peers);
            context.UdpClient.SendAsync(sendbytes, sendbytes.Length, remote);
        }
    }
}