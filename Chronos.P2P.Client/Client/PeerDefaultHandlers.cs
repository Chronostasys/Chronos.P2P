using Chronos.P2P.Server;
using System;

namespace Chronos.P2P.Client
{
    public class PeerDefaultHandlers
    {
        private Peer peer;

        public PeerDefaultHandlers(Peer peer)
        {
            this.peer = peer;
        }

        [Handler((int)CallMethods.Ack)]
        public void AckHandler(UdpContext context)
        {
            var id = context.GetData<Guid>();
            peer.AckReturned(id.Data);
        }

        [Handler((int)CallMethods.Connected)]
        public void ConnectedDataHandler(UdpContext context)
        {
            peer.PeerConnectedReceived();
        }

        [Handler((int)CallMethods.DataSlice)]
        public void FileDataHandler(UdpContext context)
        {
            _ = peer.FileDataReceived(context);
        }

        [Handler((int)CallMethods.FileHandShake)]
        public void FileHandShakeHandler(UdpContext context)
        {
            _ = peer.FileTransferRequested(context);
        }

        [Handler((int)CallMethods.P2PPing)]
        public void PingHandeler(UdpContext context)
        {
            peer.ResetPingCount();
        }

        [Handler((int)CallMethods.PunchHole)]
        public void PunchingDataHandler(UdpContext context)
        {
            peer.PunchDataReceived();
        }
    }
}