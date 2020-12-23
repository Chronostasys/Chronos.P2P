using System;
using MessagePack;

namespace Chronos.P2P.Client
{
    [MessagePackObject]
    public struct BasicFileInfo
    {
        [Key(0)]
        public long Length { get; set; }
        [Key(1)]
        public string Name { get; set; }
        [Key(2)]
        public Guid SessionId { get; set; }
    }
}