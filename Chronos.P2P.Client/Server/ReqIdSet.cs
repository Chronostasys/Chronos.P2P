using System;
namespace Chronos.P2P.Server
{
    /// <summary>
    /// 存放请求的id信息和请求时间
    /// </summary>
    internal record ReqIdSet(Guid ReqId, DateTime Time);
}