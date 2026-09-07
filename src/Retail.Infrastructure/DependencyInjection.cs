using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Retail.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // En Etapa 0.7 se registrará el DbContext y los servicios de infraestructura
        _ = configuration;
        return services;
    }
}
