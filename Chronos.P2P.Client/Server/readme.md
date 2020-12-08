#  server

## P2PServer
udp服务器

`public class P2PServer : IDisposable`

在C#中，网络连接资源不可被gc管理，故需实现内存管理。  

且有field或property实现`IDisposable`的类也需实现，这里是udp的实现中需要
`public class UdpClient : IDisposable`
### field
#### attribute

实现反射，进行属性的注册

#### info
`ConcurrentDictionary<Guid, DateTime> guidDic = new();`

通过线程安全的字典记录guid和时间，同理还有`peers`保存参与P2P通讯的`peer`信息与`Guid`

### property
#### StartServerAsync()
`public async Task StartServerAsync()`
启动消息接收的循环
- 若`serviceProvider`不存在，通过`ConfigureService()`方法注入依赖
- 启动一个线程，每10秒自动清除掉已经结束超过10秒的reliable请求id
- 不断处理接收到消息

  - 将得到数据反序列化得到消息，通过此前`AddHandler()`时建立的字典，得到消息中对应的数据类型
  - 带有reqid的请求是reliable 的请求，需要在处理请求前返回ack消息
    - [^_^]: # (疑问，对于丢包的处理)
    - 如果过程中判断`guids`里边包含此次的请求id，则说明之前已经处理过这个请求，但是我们返回的ack丢包了，不处理
    - 将`guids`加入`guidDic`

  - 调用`CallHandler()`，进而调用接收消息中相应类型请求的处理函数。
#### CallHandler()
`internal void CallHandler(TypeData data, UdpContext param)`

调用`GetInstance()`,使用反射在运行时获取对应的Handler类


[^_^]: # (疑问，getInstance的返回值问题，Activator.CreateInstance)
#### AddHandler()
`public void AddHandler<T>() where T : class`
注册处理类，保存类里处理方法的反射信息,`<T>`为泛型，可为任意类型的T进行实现

[^_^]: # (疑问，var attr = Attribute.GetCustomAttribute\(item, attribute\) as HandlerAttribute;)

####  ConfigureServices()
`public void ConfigureServices(Action<ServiceCollection> configureAction)`
-  类似 `asp.net core`的设计，用于依赖注入

 -  默认会注入自身为单例服务
## TypeData
存储注册`handler`时反射获取的类型信息
利用`System.Reflection`的`record`

## CallServerDto
标准udp请求的dto，在传输中完成序列化与反序列化  
### 序列化

```序列化
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CallServerDto<T>
            {
                Method = method,
                Data = data,
                ReqId = reqId
            });
```        


###  反序列化
```
        public CallServerDto<T> GetData<T>()
        {
            return JsonSerializer.Deserialize<CallServerDto<T>>(data);
        }
```

## HandlerAttribute
 用于标记处理程序的attribute，类似asp.net core中的<code>HttpGetAttribute</code>等.
 这个方法能处理的udp请求类型码，内置的一些请求码见`CallMethods`。
 - 内置请求码从1107开始， 自行设计请求码的时候请避免重复


## ServerHandlers
 默认的p2p.server请求处理类 
   - 已有处理：Connect
   - 对客户端建立连接的请求进行处理
  
## CallMethods
内部库常用的udp呼叫方法enum

```   
 public enum CallMethods
    {
        Connect = 1107,
        PunchHole,
        Connected,
        P2PPing,
        P2PDataTransfer,
        Ack,
        DataSlice,
        FileHandShake,
        FileHandShakeCallback
    }
    
```