using System;
using System.Runtime.InteropServices;

namespace Chronos.P2P.Client
{
    public struct DataSlice
    {
        public bool Last;
        public int Len;
        public long No;
        public Guid SessionId;
        public byte[] Slice;

        public static DataSlice FromBytes(byte[] bytes)
        {
            var slice = new DataSlice();
            var span = new ReadOnlySpan<byte>(bytes);
            slice.Last = MemoryMarshal.Read<bool>(span.Slice(0, 1));
            slice.Len = MemoryMarshal.Read<int>(span.Slice(1, 4));
            slice.No = MemoryMarshal.Read<long>(span.Slice(5, 8));
            slice.SessionId = MemoryMarshal.Read<Guid>(span.Slice(13, 16));
            slice.Slice = span[29..].ToArray();
            return slice;
        }
    }
}