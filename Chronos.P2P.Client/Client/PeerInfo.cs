using System;

namespace Chronos.P2P.Client
{
    public class PeerInfo
    {
        public DateTime CreateTime { get; }
        public Guid Id { get; set; }
        public PeerEP InnerEP { get; set; }
        public PeerEP OuterEP { get; set; }

        public PeerInfo()
        {
            CreateTime = DateTime.UtcNow;
        }
    }
}