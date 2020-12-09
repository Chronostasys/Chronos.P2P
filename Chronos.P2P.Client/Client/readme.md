# client


## field




## property

### SetPeer()
提供需要建立连接peer的guid

### SendDataToPeerReliableAsync()
向peer发送method对应类型的消息data
`public async Task<bool> SendDataToPeerReliableAsync<T>(int method, T data, CancellationToken? token = null)`

### SendFileAsync()
异步
`public async Task SendFileAsync(string location)`
