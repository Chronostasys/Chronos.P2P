using System;
using System.Collections.Generic;

namespace Chronos.P2P.Client
{
    public class PeerInfo
    {
        public DateTime CreateTime { get; }
        public Guid Id { get; set; }
        public List<PeerEP> InnerEP { get; set; }
        public PeerEP OuterEP { get; set; }

        public PeerInfo()
        {
            CreateTime = DateTime.UtcNow;
        }
    }
}