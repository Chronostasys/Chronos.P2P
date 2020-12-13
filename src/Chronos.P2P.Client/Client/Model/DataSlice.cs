using System;

namespace Chronos.P2P.Client
{
    public struct DataSlice
    {
        public bool Last { get; set; }
        public int Len { get; set; }
        public long No { get; set; }
        public Guid SessionId { get; set; }
        public byte[] Slice { get; set; }
    }
}