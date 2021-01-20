# client


## peer



### SetPeer()
提供需要建立连接peer的guid

### SendDataToPeerReliableAsync()
向peer发送method对应类型的消息data
`public async ValueTask<bool> SendDataToPeerReliableAsync<T>(int method, T data, CancellationToken? token = null)`

### SendFileAsync()
异步传输文件
`public async ValueTask SendFileAsync(string location)`


## 通讯部分
## PeerDefaultHandlers.cs
默认的p2p.client请求处理类 
 - 已有处理：
   - `Connected`：与peer建立连接过程中，接收到PunchHole消息后，发送消息
   -  `PunchHole`:与peer建立连接过程中
   -  `P2PPing`,
   -  `P2PDataTransfer`,
   -  `Ack`,
   -  `DataSlice`,
   -  `FileHandShake`,
   -  `FileHandShakeCallback`
## PeerEP.cs
记录IP和端口
## Peerlnfo
peer自身信息
```
          public DateTime CreateTime { get; }
          public Guid Id { get; set; }
          public List<PeerInnerEP> InnerEP { get; set; }
          public PeerEP OuterEP { get; set; }
```
## 传输文件部分          
## BasicFilelnfo
 传输文件信息
 ``` 
         public long Length { get; set; }
          public string Name { get; set; }
          public Guid SessionId { get; set; }
```
## DataSlice
数据分块信息
```
        public bool Last { get; set; }
        public int Len { get; set; }
        public long No { get; set; }
        public Guid SessionId { get; set; }
        public byte[] Slice { get; set; }
```
## DataSlicelnfo
通过字典`slices`与数据分块信息对应
```        public long No { get; set; }
        public Guid SessionId { get; set; }
```
## FileRecvDicData 
保存传输文件
``` 
        public string SavePath { get; set; }
        public SemaphoreSlim Semaphore { get; set; }
        public long Length { get; set; }
        public Stopwatch Watch { get; set; }
```
## FileTransferHandShakeResult
传输文件的握手过程结果
```
         public bool Accept { get; set; }
        public Guid SessionId { get; set; }
```