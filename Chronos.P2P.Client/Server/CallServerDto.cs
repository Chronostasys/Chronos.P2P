namespace Chronos.P2P.Client
{
    public class CallServerDto<TData>
    {
        public TData Data { get; set; }
        public int Method { get; set; }
    }
}