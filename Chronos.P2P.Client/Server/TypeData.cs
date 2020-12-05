using System;
using System.Reflection;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 存储注册handler时反射获取的类型信息
    /// </summary>
    public record TypeData
    {
        public Type GenericType { get; init; }
        public ParameterInfo[] Parameters { get; init; }
        public MethodInfo Method { get; init; }
    }
}