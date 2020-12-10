using Chronos.P2P.Client;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Chronos.P2P.Test
{
    public class PeerTest
    {
        private Guid id = Guid.NewGuid();
        private Peer peer;

        public PeerTest()
        {
            SetUp();
        }

        private void SetUp()
        {
            var mock = new Mock<Peer>(() => new Peer(9000, new(1000, 1000), ""));
            mock.Setup(p => p.SendDataToPeerReliableAsync(It.IsAny<int>(), It.IsAny<FileTransferHandShakeResult>(),
                    It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            peer = mock.Object;
        }

        [Fact]
        public async Task TestProcessDataSlice()
        {
            await TestTransferRequested();
            const int testlen = 10;
            for (int i = 0; i < testlen; i++)
            {
                await peer.ProcessDataSliceAsync(new DataSlice
                {
                    No = testlen - i - 1,
                    Len = Peer.bufferLen,
                    SessionId = id,
                    Last = i == 0,
                    Slice = new byte[Peer.bufferLen]
                }, () => Task.CompletedTask);
            }
            await Task.Delay(100);
            Assert.Empty(peer.slices);
            peer.Dispose();
        }

        [Fact]
        public async Task TestTransferRequested()
        {
            await peer.FileTransferRequested(new BasicFileInfo
            {
                Length = 10 * Peer.bufferLen,
                Name = $"{Guid.NewGuid()}.test",
                SessionId = id
            });
            Assert.True(peer.FileRecvDic.ContainsKey(id));
            peer.Dispose();
        }
    }
}