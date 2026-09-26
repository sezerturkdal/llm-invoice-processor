using InvoiceAi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceAi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InvoiceAi")
            ?? throw new InvalidOperationException(
                "Connection string 'InvoiceAi' is missing. Set it with 'dotnet user-secrets' or the ConnectionStrings__InvoiceAi environment variable.");

        services.AddDbContext<InvoiceAiDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        return services;
    }
}
