using System;

namespace Chronos.P2P.Client
{
    public struct DataSliceInfo
    {
        public long No { get; set; }
        public Guid SessionId { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = 37; // prime

                result *= 397; // also prime (see note)
                result += SessionId.GetHashCode();

                result *= 397;
                result += No.GetHashCode();

                return result;
            }
        }
    }
}