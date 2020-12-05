using System;

namespace Chronos.P2P.Server
{
    /// <summary>
    /// 用于标记处理程序的attribute，类似asp.net core中的<code>HttpGetAttribute</code>等等
    /// </summary>
    public class HandlerAttribute : Attribute
    {
        /// <summary>
        /// 这个方法能处理的udp请求类型码，内置的一些请求码见<see cref="Client.CallMethods"/>。
        /// 内置请求码从1107开始，
        /// 自行设计请求码的时候请避免重复
        /// </summary>
        public int Method { get; }

        public HandlerAttribute(int method)
            => Method = method;
    }
}