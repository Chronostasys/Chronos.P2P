using System.Net;

namespace Chronos.P2P.Client
{
    public class PeerEP
    {
        public string IP { get; set; }
        public int Port { get; set; }

        public static bool operator !=(PeerEP a, PeerEP b)
        {
            return !(a.IP == b.IP && a.Port == b.Port);
        }

        public static bool operator ==(PeerEP a, PeerEP b)
        {
            return a.IP == b.IP && a.Port == b.Port;
        }

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

        public override string ToString()
        {
            return $"{IP}:{Port}";
        }
    }
}