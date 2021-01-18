using Chronos.P2P.Server;
using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Chronos.P2P.Client
{
    public struct DataSlice
    {
        public bool Last;
        public int Len;
        public long No;
        public Guid SessionId;
        public Memory<byte> Slice;
        public UdpContext Context;

        public static DataSlice FromBytes(Memory<byte> bytes, UdpContext context)
        {
            var slice = new DataSlice();
            var span = bytes.Span;
            slice.Last = MemoryMarshal.Read<bool>(span.Slice(0, 1));
            slice.Len = MemoryMarshal.Read<int>(span.Slice(1, 4));
            slice.No = MemoryMarshal.Read<long>(span.Slice(5, 8));
            slice.SessionId = MemoryMarshal.Read<Guid>(span.Slice(13, 16));
            slice.Slice = bytes[29..];
            slice.Context = context;
            return slice;
        }
    }
}