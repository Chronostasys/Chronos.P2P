using System;
using System.Text.Json;

namespace Chronos.P2P.Client
{
    public class CallServerDto<TData>
    {
        public int Method { get; set; }
        public TData Data { get; set; }
        public TCast GetData<TCast>() where TCast : class
            => JsonSerializer.Deserialize<TCast>(Data.ToString());
    }
}
