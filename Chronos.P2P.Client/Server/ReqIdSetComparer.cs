using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;


namespace Chronos.P2P.Server
{
    /// <summary>
    /// 对比<see cref="ReqIdSet"/>使用的comparer，只会根据id信息比较是否相等
    /// </summary>
    internal class ReqIdSetComparer : EqualityComparer<ReqIdSet>
    {
        public override bool Equals(ReqIdSet x, ReqIdSet y)
        {
            return x.ReqId == y.ReqId;
        }

        public override int GetHashCode([DisallowNull] ReqIdSet obj)
        {
            return obj.ReqId.GetHashCode();
        }
    }
}