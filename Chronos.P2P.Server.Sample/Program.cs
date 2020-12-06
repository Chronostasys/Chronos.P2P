using Chronos.P2P.Client;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Chronos.P2P.Server.Sample
{
    public class ClientHandler
    {
        [Handler((int)CallMethods.P2PDataTransfer)]
        public void OnReceiveData(UdpContext udpContext)
        {
            var d = udpContext.GetData<string>().Data;
            if (d=="test")
            {
                Program.nums++;
            }
            Console.WriteLine("peer:"+d);
        }
    }
    class Program
    {
        public static int nums;
        static TaskCompletionSource connectionCompletionSource = new TaskCompletionSource();
        static TaskCompletionSource completionSource = new TaskCompletionSource();
        static async Task Main(string[] args)
        {
            //var peer = new Peer(8899, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000),"peer");
            //var peer1 = new Peer(8890, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000), "peer1");
            //var server = new P2PServer();
            //server.AddDefaultServerHandler();
            //server.StartServerAsync();

            //var t1 = peer.StartPeer();
            //var t2 = peer1.StartPeer();
            Console.WriteLine("enter your port:");

            var p = int.Parse(Console.ReadLine());
            var peer = new Peer(p, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            //var peer1 = new Peer(8890, new IPEndPoint(IPAddress.Parse("47.93.189.12"), 5000));
            peer.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            //peer1.PeersDataReceiveed += Peer1_PeersDataReceiveed;
            //peer1.PeerConnected += Peer1_PeerConnected;
            peer.PeerConnected += Peer1_PeerConnected;
            peer.AddHandlers<ClientHandler>();
            //peer1.AddHandlers<ClientHandler>();
            peer.StartPeer();

            //peer1.StartPeer();


            Console.WriteLine($"your peer id: {peer.ID}");
            Console.WriteLine("Waiting for handshake server...");
            await completionSource.Task;
            
            Console.WriteLine("Enter the peer id you would like to communicate to (press enter to see available peer list):");
            Guid id;
            while (!Guid.TryParse(Console.ReadLine(), out id))
            {
                foreach (var item in peer.peers)
                {
                    Console.WriteLine($"peer id: {item.Key}, innerip: {item.Value.InnerEP}, outerip: {item.Value.OuterEP}");
                }
                Console.WriteLine("Enter the peer id you would like to communicate to (press enter to see available peer list):");
            }
            peer.SetPeer(id);
            Console.WriteLine("Waiting for connection to establish...");
            await connectionCompletionSource.Task;
            Console.Clear();
            Console.WriteLine("Peer connectd!");
            while (true)
            {
                if (!await peer.SendDataToPeerReliableAsync(Console.ReadLine()))
                    Console.WriteLine("msg send failed");
            }
            
        }

        private static void Peer_PeerConnectionLost(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void Peer1_PeerConnected(object sender, EventArgs e)
        {
            connectionCompletionSource.TrySetResult();
        }

        private static void Peer1_PeersDataReceiveed(object sender, EventArgs e)
        {
            completionSource.TrySetResult();

        }
    }
}
