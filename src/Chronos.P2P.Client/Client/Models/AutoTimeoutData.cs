using System;

namespace Chronos.P2P.Client
{
    public class AutoTimeoutData
    {
        public int DevRtt { get; set; } = -1;
        public int EstimateRtt { get; set; } = -1;
        public int SendTimeOut => EstimateRtt > 0 ? EstimateRtt + 4 * Math.Max(15, DevRtt) : 5000;
    }
}