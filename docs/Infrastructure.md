# Infranstructure
设计&架构
## P2P库的架构

库重主要有两个重要的类：
- [Peer](PeerDesign.md)
- [P2PServer](P2PServerDesign.md)

其中，`P2PServer`提供了最基础的udp接收和发送的功能，并且采用了类似asp .net core的设计，可以进行依赖注入，允许使用者自行注册自己的handler。  
`Peer`代表P2P通讯的参与者。每个Peer中都封装了一个`P2PServer`。实际的信息发送和接收都是由`P2PServer`完成的。
