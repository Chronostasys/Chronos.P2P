using System;
using System.Reflection;

namespace Chronos.P2P.Server
{
    public record TypeData
    {
        public Type GenericType { get; init; }
        public ParameterInfo[] Parameters { get; init; }
        public MethodInfo Method { get; init; }
    }
}
