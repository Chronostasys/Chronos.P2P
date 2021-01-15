using MessagePack;
using System.Net;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    [MessagePackObject]
    public struct UdpMsg
    {
        [Key(0)]
        public byte[] Data { get; init; }

        [Key(1)]
        public IPEndPoint Ep { get; init; }

        [Key(2)]
        public TaskCompletionSource<bool>? SendTask { get; init; }
    }
}