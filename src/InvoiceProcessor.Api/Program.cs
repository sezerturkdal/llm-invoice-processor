using System.Text.Json.Serialization;
using InvoiceProcessor.Api.Invoices;
using InvoiceProcessor.Api.Processing;
using InvoiceProcessor.Api.Validation;
using InvoiceProcessor.Infrastructure;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

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

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InvoiceProcessorDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Malformed or oversized requests (e.g. an upload over the body size limit) are client errors, not 500s.
    StatusCodeSelector = ex => ex is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Before the HTTPS redirect, so CORS preflight requests are answered rather than redirected.
app.UseCors();
app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapInvoiceEndpoints();
app.MapInvoiceReviewEndpoints();

app.Run();
