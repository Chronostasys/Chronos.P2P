using MessagePack;
using System.Net;

namespace Chronos.P2P.Client
{
    [MessagePackObject]
    public class PeerEP
    {
        [IgnoreMember]
        private IPEndPoint? ep = null;

        [Key(0)]
        public string IP { get; init; }

        [Key(1)]
        public int Port { get; init; }

        public PeerEP()
        {
            IP = "";
        }

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

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is null)
            {
                return false;
            }
            return this == (PeerEP)obj;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = 37; // prime

                result *= 397; // also prime (see note)
                result += IP.GetHashCode();

                result *= 397;
                result += Port.GetHashCode();

                return result;
            }
        }

        public IPEndPoint ToIPEP()
        {
            if (ep is null)
            {
                ep = new IPEndPoint(IPAddress.Parse(IP), Port);
            }
            return ep!;
        }

        public override string ToString()
        {
            return $"{IP}:{Port}";
        }
    }

    [MessagePackObject]
    public class PeerInnerEP : PeerEP
    {
        [IgnoreMember]
        public IPAddress SubnetMask
        {
            get
            {
                try
                {
                    return Peer.GetSubnetMask(IPAddress.Parse(IP));
                }
                catch (System.Exception)
                {
                    return IPAddress.Parse("255.255.255.255");
                }
            }
        }

        public PeerInnerEP()
        {
        }

        public PeerInnerEP(PeerEP ep)
        {
            IP = ep.IP;
            Port = ep.Port;
        }

        public static bool operator !=(PeerInnerEP a, PeerInnerEP b)
        {
            return !(a.IP == b.IP && a.Port == b.Port && a.SubnetMask == b.SubnetMask);
        }

        public static bool operator ==(PeerInnerEP a, PeerInnerEP b)
        {
            return a.IP == b.IP && a.Port == b.Port && a.SubnetMask == b.SubnetMask;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is null)
            {
                return false;
            }

            return this == (PeerInnerEP)obj;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = 37; // prime

                result *= 397; // also prime (see note)
                result += IP.GetHashCode();

                result *= 397;
                result += Port.GetHashCode();

                result *= 397;
                result += SubnetMask.GetHashCode();

                return result;
            }
        }

        public bool IsInSameSubNet(PeerInnerEP ep)
        {
            return IPAddress.Parse(IP).IsInSameSubnet(IPAddress.Parse(ep.IP), SubnetMask);
        }
    }
}