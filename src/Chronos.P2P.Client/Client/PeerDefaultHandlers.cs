using Chronos.P2P.Server;
using System;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public class PeerDefaultHandlers
    {
        private readonly Peer peer;

        public PeerDefaultHandlers(Peer peer)
        {
            this.peer = peer;
        }

        [Handler((int)CallMethods.Connected)]
        public void ConnectedDataHandler(UdpContext context)
        {
            Console.WriteLine($"peer {peer.OuterEp?.ToIPEP()} connect data received");
            peer.PeerConnectedReceived();
        }

        [Handler((int)CallMethods.ConnectionRequestCallback)]
        public void ConnectionRequestCallbackHandler(UdpContext context)
        {
            peer.OnConnectionCallback(context.GetData<bool>());
            Console.WriteLine("received connection request callback!");
        }

        [Handler((int)CallMethods.PeerConnectionRequest)]
        public void ConnectionRequestedHandler(UdpContext context)
        {
            Console.WriteLine("received connection request!");
            _ = peer.OnConnectionRequested(context.GetData<PeerInfo>()!);
        }

        [Handler((int)CallMethods.DataSlice)]
        public void FileDataHandler(UdpContext context)
        {
            _ = peer.FileDataReceived(DataSlice.FromBytes(context.data));
        }

        [Handler((int)CallMethods.P2PPing)]
        public void PingHandeler(UdpContext context)
        {
            peer.ResetPingCount();
        }

        [Handler((int)CallMethods.PunchHole)]
        public void PunchingDataHandler(UdpContext context)
        {
            Console.WriteLine($"peer {peer.OuterEp?.ToIPEP()} punch data received");
            peer.PunchDataReceived(context);
        }

        [Handler((int)CallMethods.StartPunching)]
        public void StartPunchingHandler(UdpContext context)
        {
            peer.StartHolePunching();
            Console.WriteLine("punching started");
        }

        [Handler((int)CallMethods.StreamHandShakeCallback)]
        public void StreamHandShakeCallbackHandler(UdpContext context)
        {
            peer.OnStreamHandshakeResult(context);
        }

        [Handler((int)CallMethods.StreamHandShake)]
        public void StreamHandShakeHandler(UdpContext context)
        {
            _ = peer.StreamTransferRequested(context.GetData<BasicFileInfo>());
        }
    }
}