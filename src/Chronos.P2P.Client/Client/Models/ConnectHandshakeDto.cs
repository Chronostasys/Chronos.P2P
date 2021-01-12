using MessagePack;

namespace Chronos.P2P.Client
{
    [MessagePackObject]
    public struct ConnectHandshakeDto
    {
        [Key(0)]
        public PeerEP Ep { get; set; }

        [Key(1)]
        public PeerInfo Info { get; set; }
    }
}