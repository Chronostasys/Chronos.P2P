using System.Net;
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
            var mock = new Mock<Peer>(() => new Peer(Guid.NewGuid().GetHashCode() % 500 + 9000, new(1000, Guid.NewGuid().GetHashCode() % 500 + 8000), ""));
            mock.Setup(p => p.SendDataToPeerReliableAsync(It.IsAny<int>(), It.IsAny<FileTransferHandShakeResult>(),
                    It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.FromResult(true));
            peer = mock.Object;
        }

        [Fact]
        public async Task TestProcessDataSlice()
        {
            await TestTransferRequested();
            const int testlen = 10;
            for (int i = 0; i < testlen; i++)
            {
                peer.ProcessDataSliceAsync(new DataSlice
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
            await peer.StreamTransferRequested(new BasicFileInfo
            {
                Length = 10 * Peer.bufferLen,
                Name = $"{Guid.NewGuid()}.test",
                SessionId = id
            });
            Assert.True(peer.FileRecvDic.ContainsKey(id));
            peer.Dispose();
        }
        [Theory]
        [InlineData("192.168.1.211")]
        [InlineData("192.168.1.5")]
        [InlineData("192.168.2.211")]
        [InlineData("192.18.1.211")]
        public void TestPeerEpAutoSwitch(string ip)
        {
            peer.peer = new PeerInfo();
            try
            {
                peer.PunchDataReceived(new Server.UdpContext(
                    null, null, new IPEndPoint(IPAddress.Parse(ip), 999), null));
            }
            catch (System.Exception)
            {
            }
            Assert.True(peer.epConfirmed);
            Assert.Equal(new PeerEP{
                IP = ip,
                Port = 999
            }, peer.RmotePeer.OuterEP);
            
        }
    }
}