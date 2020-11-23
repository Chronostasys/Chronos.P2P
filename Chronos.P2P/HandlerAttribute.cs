using Chronos.P2P.Client;
using System;
using System.Collections.Generic;

namespace Chronos.P2P.Server
{
    public class HandlerAttribute:Attribute
    {
        public ServerMethods Method { get; }
        public HandlerAttribute(ServerMethods method)
            => Method = method;
    }
}
