using Chronos.P2P.Client;
using System;
using System.Collections.Generic;

namespace Chronos.P2P.Server
{
    public class HandlerAttribute:Attribute
    {
        public CallMethods Method { get; }
        public HandlerAttribute(CallMethods method)
            => Method = method;
    }
}
