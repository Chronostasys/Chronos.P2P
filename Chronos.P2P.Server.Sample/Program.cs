using Chronos.P2P.Client;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Chronos.P2P.Server.Sample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var peer = new Peer(8899, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11000));
            var peer1 = new Peer(8890, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11000));
            var server = new P2PServer();
            await server.StartServerAsync();
        }
    }
}
