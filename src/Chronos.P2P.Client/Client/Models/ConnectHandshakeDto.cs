namespace Chronos.P2P.Client
{
    internal struct ConnectHandshakeDto
    {
        public PeerEP Ep { get; set; }
        public PeerInfo Info { get; set; }
    }
}