using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;


namespace Retail.Infrastructure.Persistence.Context
{
    /// <summary>
    /// Factory used by EF Core tools at design time to create an instance of <see cref="RetailDbContext"/>.
    /// It reads the connection string from the project's "appsettings.Development.json" if present,
    /// otherwise falls back to a default LocalDB connection.
    /// </summary>
    public class RetailDbContextFactory : IDesignTimeDbContextFactory<RetailDbContext>
    {
        public RetailDbContext CreateDbContext(string[] args)
        {
            // Build a temporary configuration to obtain the connection string.
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("RetailDatabase")
                ?? "Server=.;Database=RetailDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            var optionsBuilder = new DbContextOptionsBuilder<RetailDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new RetailDbContext(optionsBuilder.Options);
        }
    }
}
