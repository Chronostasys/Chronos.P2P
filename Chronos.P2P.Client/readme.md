# Chronos.P2P.Client
实现udp协议下的P2P通讯

## [server](./Server)
### 结构
- P2PServer.cs
  - 实现P2P通讯的udp服务器
- ServerHandlers.cs
  - 默认的p2p.server请求处理类 
   - 已有处理：
     - `Connect`：接收客户端peer的连接请求
  
- CallServerDto.cs
  - 标准udp请求的dto，在传输中完成序列化与反序列化  

- HandlerAttribute.cs
    - 用于标记处理程序的attribute，类似asp.net core中的<code>HttpGetAttribute</code>等等

- CallMethods.cs
  - 内部库常用的udp呼叫方法enum
- TypeData.cs
  - 存储注册handler时反射获取的类型信息
- UdpContext.cs
  - udp请求上下文，是handler的参数。
- UdpRequest.cs
  - 部分反序列化用的类


## [client](./Client)
### 结构
- Peer.cs
  - 实现P2P通讯的udp客户端
  
- 通讯部分
  - PeerDefaultHandlers.cs
    - 默认的p2p.client请求处理类 
     - 已有处理：
       - `Connected`：与peer建立连接过程中，接收到PunchHole消息后，发送消息
       -  `PunchHole`:与peer建立连接过程中
       -  `P2PPing`,
       -  `P2PDataTransfer`,
       -  `Ack`,
       -  `DataSlice`,
       -  `FileHandShake`,
       -  `FileHandShakeCallback`
  - PeerEP.cs
    - 记录IP和端口
  - Peerlnfo.cs
    - peer自身信息
- 传输文件部分          
  - BasicFilelnfo.cs
    - 传输文件信息
  - DataSlice.cs
    - 数据分块信息
  - DataSlicelnfo.cs
    - 通过字典`slices`与数据分块信息对应
  - FileRecvDicData.cs  
    - 保存传输文件
  - FileTransferHandShakeResult.cs
    - 传输文件的握手过程结果