using System.Collections.Concurrent;

namespace Chronos.P2P.Client
{
    public class AutoTimeoutData
    {
        public ConcurrentQueue<long> Rtts { get; set; } = new ConcurrentQueue<long>();
        public int SendTimeOut { get; set; } = 5000;
    }
}