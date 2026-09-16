using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services;

namespace Retail.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<UsuarioService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IClienteService, ClienteService>();

        return services;
    }
}

