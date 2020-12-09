using Chronos.P2P.Client;
using Moq;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Chronos.P2P.Test
{
    public class PeerTest
    {
        Peer peer;
        Guid id = Guid.NewGuid();
        public PeerTest()
        {
            SetUp();
        }
        void SetUp()
        {
            var mock = new Mock<Peer>(() => new Peer(9000, new(1000, 1000), ""));
            mock.Setup(p => p.SendDataToPeerReliableAsync(It.IsAny<int>(), It.IsAny<FileTransferHandShakeResult>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            peer = mock.Object;
        }
        [Fact]
        public async Task TestTransferRequested()
        {
            await peer.FileTransferRequested(new BasicFileInfo
            {
                Length = 100,
                Name = "1.test",
                SessionId = id
            });
            Assert.True(peer.FileRecvDic.ContainsKey(id));
            peer.Dispose();
        }
        [Fact]
        public async Task TestProcessDataSlice()
        {
            await TestTransferRequested();
            const int testlen = 10;
            int count = 0;
            for (int i = 0; i < testlen; i++)
            {
                await peer.ProcessDataSliceAsync(new DataSlice
                {
                    No = testlen - i - 1,
                    Len = Peer.bufferLen,
                    SessionId = id,
                    Last = false,
                    Slice = new byte[Peer.bufferLen]
                }, dataSLice=> 
                {
                    Assert.Equal(dataSLice.No, count);
                    count++;
                }, ()=>Task.CompletedTask);
            }
            await Task.Delay(100);
            Assert.Equal(testlen, count);
            peer.Dispose();
        }


    }
}