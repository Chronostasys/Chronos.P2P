using System;

namespace Chronos.P2P.Client
{
    public class DatasliceSender
    {
        private Action<DataSlice>? sendAction;
        private Guid sessionId;

        internal void SetUp(Action<DataSlice> action, Guid guid)
        {
            sendAction = action;
            sessionId = guid;
        }

        public void Send(byte[] data, int len)
        {
            var slice = new DataSlice
            {
                No = -1,
                Slice = data,
                Len = len,
                Last = false,
                SessionId = sessionId
            };
            sendAction!(slice);
        }
    }
}