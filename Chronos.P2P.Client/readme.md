# Chronos.P2P.Client
实现udp协议下的P2P通讯

## [server](./Server)
### 结构
- P2PServer.cs
  - udp服务器
- CallServerDto.cs
  - 标准udp请求的dto，在传输中完成序列化与反序列化  

- HandlerAttribute.cs
    - 用于标记处理程序的attribute，类似asp.net core中的<code>HttpGetAttribute</code>等等
- ServerHandlers.cs
  - 默认的p2p.server请求处理类 
   - 已有处理：Connect
  
- CallMethods.cs
  - 内部库常用的udp呼叫方法enum
- TypeData.cs
  - 存储注册handler时反射获取的类型信息
- UdpContext.cs
  - udp请求上下文，是handler的参数。
- UdpRequest.cs
  - 部分反序列化用的类


## [client](./Client)







