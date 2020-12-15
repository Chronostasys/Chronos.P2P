namespace Chronos.P2P.Client
{
    internal struct ConnectionReplyDto
    {
        public bool Acc { get; set; }
        public PeerEP Ep { get; set; }
    }
}