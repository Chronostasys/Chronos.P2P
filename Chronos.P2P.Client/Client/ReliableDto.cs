using System;

namespace Chronos.P2P.Client
{
    public class ReliableDto<T> where T : class
    {
        public T Data { get; set; }
        public Guid ReqId { get; set; }
    }
}