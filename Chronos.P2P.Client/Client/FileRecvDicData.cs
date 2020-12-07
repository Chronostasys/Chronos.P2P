using System.Threading;

namespace Chronos.P2P.Client
{
    public struct FileRecvDicData
    {
        public string SavePath { get; set; }
        public SemaphoreSlim Semaphore { get; set; }
        public long Length { get; set; }
    }
}