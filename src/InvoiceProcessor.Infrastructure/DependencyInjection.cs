using Anthropic;
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

        return services;
    }

    // The provider is picked from configuration alone; adding one means a new IInvoiceExtractor and a case here.
    private static void AddInvoiceExtractor(this IServiceCollection services, IConfiguration configuration)
    {
        var llmOptions = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();

        switch (llmOptions.Provider)
        {
            case AnthropicInvoiceExtractor.ProviderName:
                // Falls back to the ANTHROPIC_API_KEY environment variable when Llm:ApiKey is not set.
                var client = string.IsNullOrWhiteSpace(llmOptions.ApiKey) ? new AnthropicClient() : new AnthropicClient { ApiKey = llmOptions.ApiKey };
                services.AddSingleton<IInvoiceExtractor>(new AnthropicInvoiceExtractor(client, llmOptions));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Llm:Provider '{llmOptions.Provider}'. Supported: {AnthropicInvoiceExtractor.ProviderName}.");
        }
    }
}
