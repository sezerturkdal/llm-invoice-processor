using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceProcessor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InvoiceProcessor")
            ?? throw new InvalidOperationException(
                "Connection string 'InvoiceProcessor' is missing. Set it with 'dotnet user-secrets' or the ConnectionStrings__InvoiceProcessor environment variable.");

        services.AddDbContext<InvoiceProcessorDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        return services;
    }
}
