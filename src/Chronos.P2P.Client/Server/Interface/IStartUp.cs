using Microsoft.Extensions.DependencyInjection;

namespace Chronos.P2P.Server
{
    public interface IStartUp
    {
        public void Configure(IRequestHandlerCollection handlers);

        public void ConfigureServices(IServiceCollection services);
    }
}