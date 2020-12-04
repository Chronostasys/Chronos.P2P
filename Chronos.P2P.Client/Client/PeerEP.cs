using System.Net;

namespace Chronos.P2P.Client
{
    public class PeerEP
    {
        public string IP { get; set; }
        public int Port { get; set; }

        public static PeerEP ParsePeerEPFromIPEP(IPEndPoint ep)
        {
            return new PeerEP
            {
                IP = ep.Address.ToString(),
                Port = ep.Port
            };
        }

        public IPEndPoint ToIPEP()
        {
            return new IPEndPoint(IPAddress.Parse(IP), Port);
        }
    }
}