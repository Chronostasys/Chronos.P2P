# Chronos.P2P.Server.Sample
运行示例

## issue
输入自身id异常退出

## 流程
### server
```
        static async Task StartServerAsync()
        {
            var server = new P2PServer();
            server.AddDefaultServerHandler();
            await server.StartServerAsync();
        }
```
1. 实例化P2PServer
2. 注册默认服务
3. 循环接收消息
### client
1. 获得用户端口
2. 创建peer
3. 获取在线所有peer 
4. 输入选定peer的guid尝试连接选定peer 
   1. `SetPeer()`进行设置
   2. 通过此前启动的异步线程`StartPeer()`与peer建立连接
       - `StartReceiveData()`：通过监听消息，更新peers数据
        -  `StartBroadCast()`：这里将带有自身info的消息向server发送
        -  `StartHolePunching()`:两个peer之间打洞；发送PunchHole，最先接收到发送Connected，收到Connected后连接成功
    3. 与连接后的peer进行通讯



### Q&A
[#]: # (疑问，peers内容的获取，peer的生命周期)
#### peers内容的获取
- peer向服务器发送的消息中（通过`StartBroadCast()`实现）有peerInfo
- `HandleConnect()`实现在udpcontext中的data里，通过反序列化得到peer，并加入字典peers中；
- 将更新后的peers字典做为数据发送至此前的peer

#### 下划线
```_ = peer.StartPeer();```

意思是接收返回值但是不使用
这是个异步方法，不await或者接收返回值的话会有warning
这么做是为了消除warning
#### endpoint 
端口

#### peer的生命周期

TODO

#### `tokenSource.IsCancellationRequested`的意义

TODO


#### TaskCompletionSource的理解
```
        static TaskCompletionSource connectionCompletionSource = new TaskCompletionSource();
        static TaskCompletionSource completionSource = new TaskCompletionSource();
```


TaskCompletionSource<T>这是一种受你控制创建Task的方式。你可以使Task在任何你想要的时候完成，你也可以在任何地方给它一个异常让它失败

[#]: #(当取对象时，才进行任务)


```         
       peer.PeersDataReceiveed += Peer1_PeersDataReceiveed;
                peer.PeerConnected += Peer1_PeerConnected;
```
监听获取到数据的事件，这样可以从获得的数据里选择连接对象




