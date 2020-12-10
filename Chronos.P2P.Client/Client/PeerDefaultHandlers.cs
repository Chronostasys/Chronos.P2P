﻿using Chronos.P2P.Server;
using System;
using System.Threading.Tasks;

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
            Console.WriteLine($"peer {peer.OuterEp?.ToIPEP()} connect data received");
            peer.PeerConnectedReceived();
        }

        [Handler((int)CallMethods.DataSlice)]
        public void FileDataHandler(UdpContext context)
        {
            Task.Run(() =>
            {
                _ = peer.FileDataReceived(context.GetData<DataSlice>().Data);
            });
        }

        [Handler((int)CallMethods.FileHandShakeCallback)]
        public void FileHandShakeCallbackHandler(UdpContext context)
        {
            peer.OnFileHandshakeResult(context);
        }

        [Handler((int)CallMethods.FileHandShake)]
        public void FileHandShakeHandler(UdpContext context)
        {
            _ = peer.FileTransferRequested(context.GetData<BasicFileInfo>().Data);
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
    }
}