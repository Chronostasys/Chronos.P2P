namespace Chronos.P2P.Client
{
    /// <summary>
    /// 內部库常用的udp呼叫方法enum
    /// </summary>
    public enum CallMethods
    {
        Connect = 1107,
        PunchHole,
        Connected,
        P2PPing,
        P2PDataTransfer,
        Ack,
        DataSlice,
        StreamHandShake,
        StreamHandShakeCallback,
        AudioDataSlice,
        ConnectionHandShake,
        ConnectionHandShakeReply,
        ConnectionRequestCallback,
        PeerConnectionRequest,
        StartPunching,
        /// <summary>
        /// A spectial kind of call, which will return an ack but not handled
        /// </summary>
        Abort,
        MTU
    }
}