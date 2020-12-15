using Chronos.P2P.Client;
using System;
using System.Text.Json;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 默认的p2pserver请求处理类
    /// </summary>
    public class ServerHandlers
    {
        private P2PServer server;

        public ServerHandlers(P2PServer _server)
        {
            server = _server;
        }

        [Handler((int)CallMethods.ConnectionHandShake)]
        public void ConnectHandShake(UdpContext context)
        {
            var data = context.GetData<ConnectHandshakeDto>().Data!;
            var ep = data.Ep;
            data.Info.OuterEP = PeerEP.ParsePeerEPFromIPEP(context.RemoteEndPoint);
            server.connectionDic[data.Info.OuterEP] = (ep, DateTime.UtcNow);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<PeerInfo>
            {
                Method = (int)CallMethods.PeerConnectionRequest,
                Data = data.Info
            });
            server.msgs.Enqueue(new UdpMsg
            {
                Data = bytes,
                Ep = ep.ToIPEP()
            });
            Console.WriteLine("send handshake data");
        }

        [Handler((int)CallMethods.Connect)]
        public void HandleConnect(UdpContext context)
        {
            var peer = context.GetData<PeerInfo>().Data!;

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

            Console.WriteLine($"receive peer {peer.Id} from {peer.OuterEP.ToIPEP()}");

            var sendbytes = JsonSerializer.SerializeToUtf8Bytes(peers);
            server.msgs.Enqueue(new UdpMsg
            {
                Data = sendbytes,
                Ep = remote
            });
        }

        [Handler((int)CallMethods.ConnectionHandShakeReply)]
        public void HolePunchRequest(UdpContext context)
        {
            var reply = context.GetData<ConnectionReplyDto>().Data!;
            var ep = reply.Ep;
            var rep = PeerEP.ParsePeerEPFromIPEP(context.RemoteEndPoint);
            Console.WriteLine("handshake reply get");
            var c = server.connectionDic.TryRemove(ep, out var t);
            if (c && t.Item1 == rep)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<bool>
                {
                    Method = (int)CallMethods.ConnectionRequestCallback,
                    Data = reply.Acc
                });
                server.msgs.Enqueue(new UdpMsg
                {
                    Data = bytes,
                    Ep = ep.ToIPEP()
                });
                if (reply.Acc)
                {
                    bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<string>
                    {
                        Method = (int)CallMethods.StartPunching,
                        Data = ""
                    });
                    server.msgs.Enqueue(new UdpMsg
                    {
                        Data = bytes,
                        Ep = ep.ToIPEP()
                    });

                    server.msgs.Enqueue(new UdpMsg
                    {
                        Data = bytes,
                        Ep = rep.ToIPEP()
                    });
                }
            }
        }
    }
}