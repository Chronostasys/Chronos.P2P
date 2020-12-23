using System;
using System.Collections.Generic;
using MessagePack;

namespace Chronos.P2P.Client
{
    [MessagePackObject]
    public class PeerInfo
    {
        [Key(0)]
        public DateTime CreateTime { get; }
        [Key(1)]
        public Guid Id { get; set; }
        [Key(2)]
        public List<PeerInnerEP> InnerEP { get; set; }
        [Key(3)]
        public PeerEP OuterEP { get; set; }

        public PeerInfo()
        {
            CreateTime = DateTime.UtcNow;
            InnerEP = new List<PeerInnerEP>();
            OuterEP = new PeerEP();
        }
    }
}