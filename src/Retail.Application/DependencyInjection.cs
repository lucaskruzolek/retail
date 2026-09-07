using Microsoft.Extensions.DependencyInjection;

namespace Retail.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // En Etapa 0.4 se registrarán los validadores de FluentValidation y servicios de caso de uso
        return services;
    }
}
