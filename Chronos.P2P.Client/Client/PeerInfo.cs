using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public class PeerInfo
    {
        public Guid Id { get; set; }
        public PeerEP InnerEP { get; set; }
        public PeerEP OuterEP { get; set; }
        public DateTime CreateTime { get; }
        public PeerInfo()
        {
            CreateTime = DateTime.UtcNow;
        }

    }
}