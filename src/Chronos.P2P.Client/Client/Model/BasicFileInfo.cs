using System;

namespace Chronos.P2P.Client
{
    public struct BasicFileInfo
    {
        public long Length { get; set; }
        public string Name { get; set; }
        public Guid SessionId { get; set; }
    }
}