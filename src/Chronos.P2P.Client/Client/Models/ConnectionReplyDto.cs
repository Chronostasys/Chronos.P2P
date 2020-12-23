using MessagePack;
namespace Chronos.P2P.Client
{
    [MessagePackObject]
    public struct ConnectionReplyDto
    {
        [Key(0)]
        public bool Acc { get; set; }
        [Key(1)]
        public PeerEP Ep { get; set; }
    }
}