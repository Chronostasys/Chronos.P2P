using System;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 存放请求的id信息和请求时间
    /// </summary>
    internal record ReqIdSet(Guid ReqId, DateTime Time) 
    {
        public override int GetHashCode()
        {
            unchecked
            {
                int result = 37; // prime

                result *= 397; // also prime (see note)
                result += ReqId.GetHashCode();

                result *= 397;
                result += Time.GetHashCode();

                return result;
            }
        }
    }
}