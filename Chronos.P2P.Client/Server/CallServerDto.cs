using System;

namespace Chronos.P2P.Client
{
    /// <summary>
    /// 标准udp请求的dto
    /// </summary>
    /// <typeparam name="TData">真正的数据类型</typeparam>
    public class CallServerDto<TData>
    {
        public TData Data { get; set; }
        public int Method { get; set; }
        public Guid ReqId { get; set; }
    }
}