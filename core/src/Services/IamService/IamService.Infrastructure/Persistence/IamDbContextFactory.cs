using IamService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IamService.Infrastructure.Persistence;

public class IamDbContextFactory : IDesignTimeDbContextFactory<IamDbContext>
{
    public IamDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../IamService.API"))
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = NpgsqlIamConfiguration.CreateDataSource(connectionString);
        var optionsBuilder = new DbContextOptionsBuilder<IamDbContext>();
        optionsBuilder.UseNpgsql(dataSource, npgsql =>
        {
            npgsql.MapEnum<LoginProvider>("login_provider");
            npgsql.MapEnum<UserStatus>("user_status");
            npgsql.MapEnum<UserRole>("user_role");
        });

        return new IamDbContext(optionsBuilder.Options);
    }
}
