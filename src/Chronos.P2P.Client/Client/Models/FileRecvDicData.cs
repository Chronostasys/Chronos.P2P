using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public struct FileRecvDicData
    {
        public long Length { get; init; }
        public string SavePath { get; init; }
        public Progress SendProgress { get; init; }
        public SemaphoreSlim Semaphore { get; init; }
        public ValueTask LastWriteTask { get; set; }
        public Memory<byte> WriteBuffer { get; set; }
        public Memory<byte> FSWriteBuffer { get; set; }
        public Stopwatch Watch { get; init; }
        public FileStream FS { get; init; }
        public long Total { get; init; }
    }
}