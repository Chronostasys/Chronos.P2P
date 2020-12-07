using System;

namespace Chronos.P2P.Client
{
    public struct FileTransferHandShakeResult
    {
        public bool Accept { get; set; }
        public Guid SessionId { get; set; }
    }
}