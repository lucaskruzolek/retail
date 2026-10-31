using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services;

namespace Retail.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Los validadores de reportes calculan "hoy" con el reloj inyectado (D-9). Se registra solo si el Host no puso otro.
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<UsuarioService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<IVentaService, VentaService>();
        services.AddScoped<IReporteService, ReporteService>();
        services.AddScoped<IProveedorService, ProveedorService>();
        services.AddScoped<ICompraService, CompraService>();
        services.AddScoped<IRubroService, RubroService>();
        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IMarcaService, MarcaService>();
        services.AddScoped<IClasificacionService, ClasificacionService>();
        services.AddScoped<ITerminoClasificacionService, TerminoClasificacionService>();
        services.AddScoped<IEquivalenciaClasificacionService, EquivalenciaClasificacionService>();
        services.AddScoped<IEstadoSistemaService, EstadoSistemaService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IExportacionReportesService, ExportacionReportesService>();
        services.AddScoped<IPedidoService, PedidoService>();

        // La aplicación y Retail.Cli comparten el mecanismo (D-21). No requiere ser una operación privilegiada: toda
        // restauración exige la contraseña de un Gerente.
        services.AddScoped<IRestauracionService, RestauracionService>();

        return services;
    }

    /// <summary>
    /// Operaciones privilegiadas de administración de la base (RF-23). Solo la invoca la herramienta de comandos:
    /// la aplicación WPF no las registra y, por lo tanto, no puede ejecutarlas.
    /// </summary>
    public static IServiceCollection AddAdministracionSistema(this IServiceCollection services)
    {
        services.AddScoped<IAdministracionSistemaService, AdministracionSistemaService>();

        return services;
    }
}
