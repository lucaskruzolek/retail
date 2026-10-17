using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Backups;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.ExternalServices.ArcaSdk;
using Retail.Infrastructure.Hardware;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Security;

namespace Retail.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Opciones y servicios de Hardware / Mocks (Etapa 0.5)
        services.Configure<TicketPrinterOptions>(
            configuration.GetSection(TicketPrinterOptions.SectionName));

        services.AddSingleton<ITicketPrinterService, FileDebugTicketPrinterService>();

        // 2. Opciones y cliente fiscal ARCA / AFIP
        var arcaSection = configuration.GetSection(ArcaOptions.SectionName);
        services.Configure<ArcaOptions>(arcaSection);

        var arcaOptions = arcaSection.Get<ArcaOptions>() ?? new ArcaOptions();

        if (arcaOptions.UseMockArca)
        {
            services.AddSingleton<IArcaClient, MockArcaClient>();
        }
        else
        {
            services.AddHttpClient<IArcaClient, ArcaClient>((serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ArcaOptions>>().Value;
                httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
        }

        // 3. Seguridad y Criptografía (BCrypt)
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // 4. Persistencia en Entity Framework Core 8 (SQL Server Express)
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? @"Server=.\SQLEXPRESS;Database=RetailDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        services.AddDbContext<RetailDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IRetailDbContext>(sp => sp.GetRequiredService<RetailDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ICatalogoProveedorQueryService, Retail.Infrastructure.Persistence.Services.CatalogoProveedorQueryService>();
        services.AddScoped<IArticuloQueryService, Retail.Infrastructure.Persistence.Services.ArticuloQueryService>();
        services.AddScoped<IClasificacionQueryService, Retail.Infrastructure.Persistence.Services.ClasificacionQueryService>();
        services.AddScoped<IClienteQueryService, Retail.Infrastructure.Persistence.Services.ClienteQueryService>();
        services.AddScoped<IReporteQueryService, Retail.Infrastructure.Persistence.Services.ReporteQueryService>();
        services.AddScoped<IEsquemaBaseDatos, Retail.Infrastructure.Persistence.Services.EsquemaBaseDatos>();

        // 5. Copias de seguridad (RF-22): directorio dedicado, motor SQL Server y política de retención.
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));
        services.AddSingleton<IArchivosBackup, ArchivosBackup>();
        services.AddScoped<IEjecutorBackupBaseDatos, SqlServerEjecutorBackup>();

        // Restauración (D-17): bloqueo entre restores y copias (sp_getapplock sobre master).
        services.AddScoped<IBloqueoRestauracion, SqlServerBloqueoRestauracion>();
        services.AddScoped<IRestauradorBaseDatos, SqlServerRestauradorBaseDatos>();
        services.AddSingleton(serviceProvider =>
        {
            var opciones = serviceProvider.GetRequiredService<IOptions<BackupOptions>>().Value;
            if (opciones.CantidadMaximaCopias < 1)
            {
                throw new InvalidOperationException(
                    $"{BackupOptions.SectionName}:CantidadMaximaCopias debe ser al menos 1: si no, la retención borraría la copia recién hecha.");
            }

            if (opciones.DiasRetencionPreRestauracion < 1)
            {
                throw new InvalidOperationException(
                    $"{BackupOptions.SectionName}:DiasRetencionPreRestauracion debe ser al menos 1: la copia previa a restaurar es la red de seguridad de los datos reemplazados.");
            }

            return new PoliticaBackup(opciones.CantidadMaximaCopias, ObtenerVersionAplicacion(), opciones.DiasRetencionPreRestauracion);
        });

        // 6. Procesamiento Streaming de Planillas Excel (MiniExcel)
        services.AddSingleton<IExcelCatalogParser, Retail.Infrastructure.ExternalServices.Excel.ExcelCatalogParser>();

        // 7. Exportación de reportes (RF-26): no tiene estado, así que una única instancia sirve a toda la aplicación.
        services.AddSingleton<IExportadorReportes, Retail.Infrastructure.Reportes.ExportadorReportes>();

        // 8. Reemplazamos el doble de prueba por la implementación real de CajaService.
        services.AddScoped<Retail.Application.Interfaces.Services.ICajaService, Retail.Application.Services.CajaService>();
        return services;
    }

    /// <summary>
    /// Versión que se registra en cada copia: la del ejecutable que la pide (Retail.App o Retail.Cli comparten la
    /// versión de <c>Directory.Build.props</c>).
    /// </summary>
    private static string ObtenerVersionAplicacion()
    {
        var ensamblado = Assembly.GetEntryAssembly() ?? typeof(DependencyInjection).Assembly;
        var version = ensamblado.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? ensamblado.GetName().Version?.ToString()
            ?? "desconocida";

        // Se descarta el sufijo "+<commit>" que agrega el SDK: alcanza con la versión del producto.
        var corte = version.IndexOf('+', StringComparison.Ordinal);
        return corte > 0 ? version[..corte] : version;
    }
}
