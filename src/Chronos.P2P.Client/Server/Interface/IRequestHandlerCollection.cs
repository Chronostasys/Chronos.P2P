namespace Chronos.P2P.Server
{
    public interface IRequestHandlerCollection
    {
        public void AddHandler<T>() where T : class;
    }
}