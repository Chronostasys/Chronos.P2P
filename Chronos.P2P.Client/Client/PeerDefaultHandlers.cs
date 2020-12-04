using Chronos.P2P.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Chronos.P2P.Client
{
    public class PeerDefaultHandlers
    {
        Peer peer;
        public PeerDefaultHandlers(Peer peer)
        {
            this.peer = peer;
        }

        [Handler((int)CallMethods.PunchHole)]
        public void PunchingDataHandler(UdpContext context)
        {
            peer.PunchDataReceived();
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
    }
}
