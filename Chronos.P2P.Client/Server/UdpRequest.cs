using System;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 部分反序列化用的类
    /// </summary>
    public class UdpRequest
    {
        public int Method { get; set; }
        public Guid ReqId { get; set; }
    }
}