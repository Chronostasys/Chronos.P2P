using Chronos.P2P.Client;
using MessagePack;
using Microsoft.Extensions.Logging;
using System;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 默认的p2pserver请求处理类
    /// </summary>
    public class ServerHandlers
    {
        private readonly P2PServer server;
        private ILogger<ServerHandlers> _logger;
        public ServerHandlers(P2PServer _server, ILogger<ServerHandlers> logger)
        {
            server = _server;
            _logger = logger;
        }

        [Handler((int)CallMethods.Ack)]
        public void AckHandler(UdpContext context)
        {
            var id = context.GetData<Guid>();
            if (server.ackTasks.TryGetValue(id, out var src))
            {
                src.TrySetResult(true);
            }
        }

        [Handler((int)CallMethods.ConnectionHandShake)]
        public async void ConnectHandShake(UdpContext context)
        {
            var data = context.GetData<ConnectHandshakeDto>();
            var ep = data.Ep;
            data.Info.OuterEP = PeerEP.ParsePeerEPFromIPEP(context.RemoteEndPoint);
            server.connectionDic[data.Info.OuterEP] = (ep, DateTime.UtcNow);
            await P2PServer.SendDataReliableAsync((int)CallMethods.PeerConnectionRequest, data.Info,
                ep.ToIPEP(), server.ackTasks, server.msgs, server.timeoutData);
            _logger.LogInformation("send handshake data");
        }

        [Handler((int)CallMethods.Connect)]
        public void HandleConnect(UdpContext context)
        {
            var peer = context.GetData<PeerInfo>()!;

            var remote = context.RemoteEndPoint;
            var peers = context.Peers;
            foreach (var item in peers)
            {
                if ((DateTime.UtcNow - item.Value.CreateTime).TotalSeconds > 10)
                {
                    peers.TryRemove(item);
                }
            }
            peers[peer.Id] = peer;
            peer.OuterEP = PeerEP.ParsePeerEPFromIPEP(remote);

            _logger.LogInformation($"receive peer {peer.Id} from {peer.OuterEP.ToIPEP()}");

            var sendbytes = MessagePackSerializer.Serialize(peers);
            server.msgs.Enqueue(new UdpMsg
            {
                Data = sendbytes,
                Ep = remote
            });
        }

        [Handler((int)CallMethods.ConnectionHandShakeReply)]
        public async void HolePunchRequest(UdpContext context)
        {
            var reply = context.GetData<ConnectionReplyDto>()!;
            var ep = reply.Ep;
            var rep = PeerEP.ParsePeerEPFromIPEP(context.RemoteEndPoint);
            _logger.LogInformation("handshake reply get");
            var c = server.connectionDic.TryRemove(ep, out var t);
            if (c && t.Item1 == rep)
            {
                var v1 = P2PServer.SendDataReliableAsync((int)CallMethods.ConnectionRequestCallback, reply.Acc,
                    ep.ToIPEP(), server.ackTasks, server.msgs, server.timeoutData);
                if (reply.Acc)
                {
                    var v2 = P2PServer.SendDataReliableAsync((int)CallMethods.StartPunching, "",
                        ep.ToIPEP(), server.ackTasks, server.msgs, server.timeoutData);
                    var v3 = P2PServer.SendDataReliableAsync((int)CallMethods.StartPunching, "",
                        rep.ToIPEP(), server.ackTasks, server.msgs, server.timeoutData);
                    await v2;
                    await v3;
                }
                await v1;
            }
        }
    }
}