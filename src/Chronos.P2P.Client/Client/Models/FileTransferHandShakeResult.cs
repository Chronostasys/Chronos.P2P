using System;
using MessagePack;

namespace Chronos.P2P.Client
{
    [MessagePackObject]
    public struct FileTransferHandShakeResult
    {
        [Key(0)]
        public bool Accept { get; set; }
        [Key(1)]
        public Guid SessionId { get; set; }
    }
}