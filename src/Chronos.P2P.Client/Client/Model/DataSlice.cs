using System.Runtime.InteropServices;
using System;

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
            slice.Slice = span.Slice(29).ToArray();
            return slice;
        }
        public byte[] ToBytes()
        {
            var dataSpan = new Span<byte>(new byte[Peer.bufferLen+29]);
            MemoryMarshal.Write(dataSpan, ref Last);
            MemoryMarshal.Write(dataSpan.Slice(1), ref Len);
            MemoryMarshal.Write(dataSpan.Slice(5), ref No);
            MemoryMarshal.Write(dataSpan.Slice(13), ref SessionId);
            var bytes = dataSpan.ToArray();
            Buffer.BlockCopy(Slice,0, bytes,29,Slice.Length);
            return bytes;
        }
    }
}