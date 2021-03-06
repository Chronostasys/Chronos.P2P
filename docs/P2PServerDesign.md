# P2PServer设计

## 大纲
`P2PServer`是我们设计的两大核心类之一。 它要能以高效率接收、发送udp请求，并且要能处理可靠和不可靠两种udp请求模式。为了方便使用，它还要易于扩展、自定义，所以我们模仿了asp .net core的设计，并且给它加入了构造函数依赖注入功能。  
[源代码](../src/chronos.P2P.client/Server/P2PServer.cs)  

## 设计细节
细节设计内容
### 处理类注册
为了让使用者可以高度自定义对不同udp请求的处理方法，我们设计了规范的请求接口。并且采用了注册处理类的模式。用户只需要自己写一个处理类，并且注册进来，请求到来的时候就会自动调用处理类里的函数。  
处理类就和普通的类一样，设计上对应asp .net core中的`Controller`，其中的处理函数必须带上`HandlerAttribute`，这个Attribute需要接受一个`CallMethod`参数，该参数决定了它会用来处理哪样的请求。
处理方法同时需要接收一个`UdpContext`类型的参数，这里边包含了书记请求的数据。处理类的构造函数可以包含参数，这些参数每次请求到来的时候会自动从依赖注入容器中获得。  
注册处理类使用`AddHandler<T>`方法。  
#### 底层
注册处理类的时候，我们会通过反射获取它的元数据信息并且保存在字典中。之后每次请求到来的时候使用元数据构造这个类，并且调用相应的处理方法。  
所以不宜在Handler的构造函数中加入大运算量的操作。如果必须用到，请考虑通过依赖注入的方式来传入运算结果。
### 依赖注入
依赖注入和asp .net core的依赖注入基本一样，用`ConfigureServices`方法来配置。`P2PServer`默认会将自身注册入容器。  
### Udp接收
接收的时候，使用了一个死循环，用`ReceiveAsync`方法不断地接收他人的消息。接收到之后，会使用线程池里的线程来进行后续处理，接收线程会直接心如下一个循环。也就是说**多个处理函数可能会并发地执行**。所以如果大消息分片，需要考虑处理函数的线程安全问题。  
#### 可靠udp
可靠的udp信息会携带一个`ReqId`参数，一个可靠udp的传输过程如下：
1. 发送方：
   1. 设定重传初始次数为0，生成一个`ReqId`
   2. 将`ReqId`带在发送数据里发送，等待ACK
   3. 若一定时间内收到ACK，则结束发送的`Task<bool>`，返回`true`。
   4. 若超过设定的最大等待时间，则重传次数++
   5. 若重传次数大于最大重传次数，则结束发送的`Task<bool>`，返回`false`。否则回到1.2
2. 接收方：
   1. 初始化一个字典，键是`Guid`类型，值是`DateTime`类型
   2. 等待接收发送方的信息
   3. 接收到信息
   4. 删除字典中所有值记录的时间早于现在10秒的键值对
   5. 若带有`ReqId`参数，返回ACK信息，带上`ReqId`
   6. 检查键为`ReqId`的信息是否在字典中
   7. 若不存在，则将键值对`ReqId:DateTime`存入字典，调用对应的Handler。
   8. 若存在，则不进行任何操作
   9. 返回第二步

可靠udp的设计和[peer](PeerDesign.md)中的接收设计合起来，可以做到高并发发送--Peer不仅可以同时相互发送多个不同的文件，即使是发送相同的文件，也可以并发发送，不需要每次等到ack信号后再发下一个。这种设计极大的提升了文件传输的效率，
比我们计网课设的tftp平均速度高300多倍，且稳定性高得多，几乎不会因为丢包多就发送失败（测试中从来没失败过，即使丢包率达到百分之50）。  
> [!Note]
> 能够高并发发送并不意味着并发越高越好。实验证明，并发数达到一定程度后，它会和丢包率成正比。并且不加限制的并发可能导致发送方电脑资源耗尽而宕机。所以在设计的时候，我们
> 给文件发送api的并发数加了限制。这个限制是可调的，默认是3。一般来讲不宜超过100。  
> 同时，用户应当注意到的一点是，并发限制数并不对应线程数。由于异步api的使用，实际的线程数可能远少于设置的并发线程数。  

> [!TIP]
> 关于丢包率  
> 尽管在普通的传输过程中，丢包率可能会对传输速度造成大的影响（比如笔者同学写tftp实验时的常见设计：每次发送完先等到ack回复再发下一个包），在使用并发设计传输文件时，丢包对传输速度的影响会比串行传输的设计小非常多。理论上并发率越高影响越不明显。  
> 然而这并不意味我们要在丢包率高的场景提高并发限制。因为更高的并发限制会增大网路压力，从而进一步提升丢包率，这会造成恶行循环。而且高丢包率也会导致接收方内存消耗不稳定。

### Udp发送
需要注意，`UdpClient`类中的方法并不是线程安全的。它能够同时接收且发送，但是不能同时在多个线程中使用发送或者接收。  
所以我设计了一个发送队列，使用一个`MsgQueue`实现了死循环的异步遍历。最大限度地增加发送速度而保证了线程安全。  
所以，实际上发送信息的操作只是将需要发送的消息enqueue。  
关于MsgQueue的细节参考[此处](MsgQueue.md)

