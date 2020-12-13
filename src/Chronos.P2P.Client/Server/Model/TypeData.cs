using System;
using System.Reflection;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 存储注册handler时反射获取的类型信息
    /// </summary>
    public record TypeData(Type GenericType, ParameterInfo[] Parameters, MethodInfo? Method);
}