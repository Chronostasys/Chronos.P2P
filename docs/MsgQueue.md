# MsgQueue

`MsgQueue<T>`，一个class，对`ConcurrentQueue<T>`进行了封装。它是线程安全的，而且实现了`IAsyncEnumerable`接口。  
[源代码](../src/chronos.P2P.client/Client/Model/MsgQueue.cs)  
它使用`semaphore`，实现了异步等待。每次只有队列里有信息的时候，`DequeAsync`方法才能执行。否则该方法会一直异步等待到新信息入队列再执行完。  
该类可以使用`await foreach`语法，但是要注意：该循环不会自然终止，只能通过传入`CancelationToken`来终止。详细信息可以查看[这里](../src/Chronos.P2P.Test/Unit/Client/MsgQueueTest.cs)的`TestIAsyncEnumerableCancel`方法

