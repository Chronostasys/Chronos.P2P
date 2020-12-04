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
        Timer timer = new Timer(10000);
        bool connectionLoss = false;
        Peer peer;
        public PeerDefaultHandlers(Peer peer)
        {
            timer.Elapsed += Timer_Elapsed;
            this.peer = peer;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (connectionLoss)
            {

            }
            connectionLoss = true;
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
            connectionLoss = false;
        }
    }
}
