using System;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Chronos.P2P.Client
{
    public class AutoTimeoutData
    {
        public int SendTimeOut => EstimateRtt > 0 ? EstimateRtt + 4 * Math.Max(15, DevRtt) : 5000;
        public int DevRtt { get; set; } = -1;
        public int EstimateRtt { get; set; } = -1;

    }
}