using ExamCatalogService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ExamCatalogService.Infrastructure.Persistence;

public class ExamCatalogDbContextFactory : IDesignTimeDbContextFactory<ExamCatalogDbContext>
{
    public ExamCatalogDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ExamCatalogService.API"))
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = NpgsqlExamCatalogConfiguration.CreateDataSource(connectionString);
        var optionsBuilder = new DbContextOptionsBuilder<ExamCatalogDbContext>();
        optionsBuilder.UseNpgsql(dataSource, npgsql =>
        {
            npgsql.MapEnum<ExamType>("exam_type");
            npgsql.MapEnum<SubjectStatus>("subject_status");
        });

        return new ExamCatalogDbContext(optionsBuilder.Options);
    }
}
