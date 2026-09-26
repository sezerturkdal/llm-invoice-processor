using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Infrastructure.Persistence;
using InvoiceProcessor.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoiceProcessor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString("InvoiceProcessor")
            ?? throw new InvalidOperationException(
                "Connection string 'InvoiceProcessor' is missing. Set it with 'dotnet user-secrets' or the ConnectionStrings__InvoiceProcessor environment variable.");

        services.AddDbContext<InvoiceProcessorDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.TryAddSingleton(TimeProvider.System);

        var storageOptions = configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>() ?? new FileStorageOptions();
        var storageRoot = Path.GetFullPath(storageOptions.RootPath, contentRootPath);
        services.AddSingleton<IFileStorage>(sp => new LocalFileStorage(storageRoot, sp.GetRequiredService<TimeProvider>()));

        return services;
    }
}
