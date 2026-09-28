using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Infrastructure.Extraction;
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

        services.AddInvoiceExtractor(configuration);
        services.AddSingleton<ExtractionModelCatalog>();
        services.AddScoped<ExtractionModelSelector>();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="InvoiceExtractorFactory"/> and, as <see cref="IInvoiceExtractor"/>, the extractor
    /// for <c>Llm:Provider</c> and <c>Llm:Model</c>. Public so the eval runner builds exactly what the API runs.
    /// Adding a provider means a new IInvoiceExtractor and a case in the factory.
    /// </summary>
    public static IServiceCollection AddInvoiceExtractor(this IServiceCollection services, IConfiguration configuration)
    {
        var llmOptions = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();

        if (!InvoiceExtractorFactory.Providers.Contains(llmOptions.Provider))
        {
            throw new InvalidOperationException(
                $"Unknown Llm:Provider '{llmOptions.Provider}'. Supported: {string.Join(", ", InvoiceExtractorFactory.Providers)}.");
        }

        if (!llmOptions.IsAllowed(llmOptions.Provider))
        {
            throw new InvalidOperationException(
                $"Llm:Provider '{llmOptions.Provider}' is not in Llm:AllowedProviders ({string.Join(", ", llmOptions.AllowedProviders!)}).");
        }

        var factory = new InvoiceExtractorFactory(llmOptions);
        services.AddSingleton(factory);
        services.AddSingleton(factory.Create(llmOptions.Provider, llmOptions.Model));

        return services;
    }
}
