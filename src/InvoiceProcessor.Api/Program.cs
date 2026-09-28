using System.Text.Json.Serialization;
using InvoiceProcessor.Api.Auth;
using InvoiceProcessor.Api.Extractions;
using InvoiceProcessor.Api.Invoices;
using InvoiceProcessor.Api.Processing;
using InvoiceProcessor.Api.Settings;
using InvoiceProcessor.Api.Users;
using InvoiceProcessor.Api.Validation;
using InvoiceProcessor.Infrastructure;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddInvoiceProcessorAuth(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.SectionName));

// The React app runs on its own origin (Vite dev server, or the web container).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton<InvoiceProcessingQueue>();
builder.Services.AddScoped<InvoiceValidationService>();
builder.Services.AddScoped<InvoiceExtractionPipeline>();
builder.Services.AddHostedService<InvoiceProcessingWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await scope.ServiceProvider.GetRequiredService<InvoiceProcessorDbContext>().Database.MigrateAsync();
    }

    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Malformed or oversized requests (e.g. an upload over the body size limit) are client errors, not 500s.
    StatusCodeSelector = ex => ex is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Before the HTTPS redirect, so CORS preflight requests are answered rather than redirected.
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuthEndpoints();
app.MapInvoiceEndpoints();
app.MapInvoiceReviewEndpoints();
app.MapExtractionStatsEndpoints();
app.MapUserEndpoints();
app.MapExtractionSettingsEndpoints();

app.Run();

// Lets the integration tests host the app with WebApplicationFactory<Program>.
public partial class Program;
