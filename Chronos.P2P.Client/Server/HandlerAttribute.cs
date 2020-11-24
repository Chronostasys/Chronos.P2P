using Chronos.P2P.Client;
using System;
using System.Collections.Generic;

namespace Chronos.P2P.Server
{
    public class HandlerAttribute:Attribute
    {
        public int Method { get; }
        public HandlerAttribute(int method)
            => Method = method;
    }
}
