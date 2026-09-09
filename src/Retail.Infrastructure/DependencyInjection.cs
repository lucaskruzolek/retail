using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
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

        // 4. Persistencia en Entity Framework Core 8 (SQL Server / LocalDB)
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=RetailDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        services.AddDbContext<RetailDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IRetailDbContext>(sp => sp.GetRequiredService<RetailDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
