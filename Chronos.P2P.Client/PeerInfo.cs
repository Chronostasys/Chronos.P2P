using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.P2P.Client
{
    public class PeerInfo:IDisposable
    {
        public Guid Id { get; set; }
        public PeerEP InnerEP { get; set; }
        public PeerEP OuterEP { get; set; }
        CancellationTokenSource tokenSource = new CancellationTokenSource();

        public void Dispose()
        {
            tokenSource.Cancel();
        }

        public void SetTimeOut(ConcurrentDictionary<Guid, PeerInfo> dic)
        {
            Task.Delay(20000, tokenSource.Token)
                .ContinueWith((t) =>
                    {
                        tokenSource.Token.ThrowIfCancellationRequested();
                        while (!dic.Remove(Id, out var value));
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}
