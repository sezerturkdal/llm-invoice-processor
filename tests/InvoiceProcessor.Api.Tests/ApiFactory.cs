using System.Net.Http.Json;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace InvoiceProcessor.Api.Tests;

/// <summary>
/// The real API against a throwaway SQL Server in Docker, so authorization runs through the same
/// middleware, Identity stores and migrations as in production. Needs Docker running.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string ReviewerEmail = "reviewer@test.local";
    public const string Password = "test-password-123";

    // The same image docker-compose uses, so it is usually already pulled.
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), "invoiceprocessor-tests", Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        // Starts the host now: migrations and seeding run once for all tests.
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
        if (Directory.Exists(_uploads))
        {
            Directory.Delete(_uploads, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:InvoiceProcessor", _sql.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("FileStorage:RootPath", _uploads);
        builder.UseSetting("Llm:ApiKey", "not-used-no-extraction-runs-in-these-tests");
        builder.UseSetting("Auth:DemoUsers:0:Email", AdminEmail);
        builder.UseSetting("Auth:DemoUsers:0:Password", Password);
        builder.UseSetting("Auth:DemoUsers:0:Role", "Admin");
        builder.UseSetting("Auth:DemoUsers:1:Email", ReviewerEmail);
        builder.UseSetting("Auth:DemoUsers:1:Password", Password);
        builder.UseSetting("Auth:DemoUsers:1:Role", "Reviewer");

        builder.ConfigureTestServices(services =>
            // Check the security stamp on every request, so revoked sessions end immediately in tests.
            services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero));
    }

    /// <summary>A client with its own cookie jar, signed in as the given user.</summary>
    public async Task<HttpClient> SignedInClientAsync(string email, string password = Password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<InvoiceProcessorDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<InvoiceProcessorDbContext>());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
