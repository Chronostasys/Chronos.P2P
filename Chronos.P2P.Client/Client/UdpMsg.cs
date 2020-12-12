using System.Net;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public struct UdpMsg
    {
        public byte[] Data { get; init; }
        public IPEndPoint Ep { get; init; }
        public TaskCompletionSource? SendTask { get; init; }
    }
}