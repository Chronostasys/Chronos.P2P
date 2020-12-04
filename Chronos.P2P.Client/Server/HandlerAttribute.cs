using System;

namespace Chronos.P2P.Server
{
    public class HandlerAttribute : Attribute
    {
        public int Method { get; }

        public HandlerAttribute(int method)
            => Method = method;
    }
}