using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public struct FileRecvDicData
    {
        public Task IOTask { get; init; }
        public long Length { get; init; }
        public MsgQueue<DataSlice> MsgQueue { get; init; }
        public string SavePath { get; init; }
        public SemaphoreSlim Semaphore { get; init; }
        public Stopwatch Watch { get; init; }
    }
}