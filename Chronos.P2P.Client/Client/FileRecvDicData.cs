﻿using System.Diagnostics;
using System.Threading;

namespace Chronos.P2P.Client
{
    public struct FileRecvDicData
    {
        public long Length { get; set; }
        public string SavePath { get; set; }
        public SemaphoreSlim Semaphore { get; set; }
        public Stopwatch Watch { get; set; }
    }
}