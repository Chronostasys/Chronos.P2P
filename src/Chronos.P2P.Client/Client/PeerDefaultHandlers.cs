using Chronos.P2P.Server;
using Microsoft.Extensions.Logging;
using System;

namespace Chronos.P2P.Client
{
    public class PeerDefaultHandlers
    {
        private readonly Peer peer;
        ILogger<PeerDefaultHandlers> _logger;

        public PeerDefaultHandlers(Peer peer, ILogger<PeerDefaultHandlers> logger)
        {
            this.peer = peer;
            _logger = logger;
        }

        [Handler((int)CallMethods.Connected)]
        public void ConnectedDataHandler(UdpContext context)
        {
            _logger.LogInformation($"peer {peer.OuterEp?.ToIPEP()} connect data received");
            peer.PeerConnectedReceived();
            context.Dispose();
        }

        [Handler((int)CallMethods.ConnectionRequestCallback)]
        public void ConnectionRequestCallbackHandler(UdpContext context)
        {
            peer.OnConnectionCallback(context.GetData<bool>());
            _logger.LogInformation("received connection request callback!");
        }

        [Handler((int)CallMethods.PeerConnectionRequest)]
        public async void ConnectionRequestedHandler(UdpContext context)
        {
            _logger.LogInformation("received connection request!");
            await peer.OnConnectionRequested(context.GetData<PeerInfo>()!);
        }

        [Handler((int)CallMethods.DataSlice)]
        public async void FileDataHandler(UdpContext context)
        {
            await peer.FileDataReceived(DataSlice.FromBytes(context.Data, context));
        }

        [Handler((int)CallMethods.P2PPing)]
        public void PingHandeler(UdpContext context)
        {
            peer.ResetPingCount();
            context.Dispose();
        }
        [Handler((int)CallMethods.MTU)]
        public void MTUHandler(UdpContext udpContext)
        {
            peer.remoteBufferLen = udpContext.GetData<int>();
            peer.nSlices = 10485760 / peer.remoteBufferLen;
        }

        [Handler((int)CallMethods.PunchHole)]
        public void PunchingDataHandler(UdpContext context)
        {
            _logger.LogInformation($"peer {peer.OuterEp?.ToIPEP()} punch data received");
            peer.PunchDataReceived(context.RemoteEndPoint);
            context.Dispose();
        }

        [Handler((int)CallMethods.StartPunching)]
        public void StartPunchingHandler(UdpContext context)
        {
            peer.StartHolePunching();
            _logger.LogInformation("punching started");
            context.Dispose();
        }

        [Handler((int)CallMethods.StreamHandShakeCallback)]
        public void StreamHandShakeCallbackHandler(UdpContext context)
        {
            peer.OnStreamHandshakeResult(context);
        }

        [Handler((int)CallMethods.StreamHandShake)]
        public async void StreamHandShakeHandler(UdpContext context)
        {
            await peer.StreamTransferRequested(context.GetData<BasicFileInfo>());
        }
    }
}