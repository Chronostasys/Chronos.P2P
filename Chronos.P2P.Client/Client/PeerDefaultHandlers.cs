using Chronos.P2P.Server;

namespace Chronos.P2P.Client
{
    public class PeerDefaultHandlers
    {
        private Peer peer;

        public PeerDefaultHandlers(Peer peer)
        {
            this.peer = peer;
        }

        [Handler((int)CallMethods.Connected)]
        public void ConnectedDataHandler(UdpContext context)
        {
            peer.PeerConnectedReceived();
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