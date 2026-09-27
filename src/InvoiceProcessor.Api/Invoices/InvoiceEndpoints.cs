using InvoiceProcessor.Api.Processing;
using InvoiceProcessor.Core.Files;
using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvoiceProcessor.Api.Invoices;

public static class InvoiceEndpoints
{
    private const int MaxPageSize = 100;

    // Room for the multipart boundaries and part headers around the file itself.
    private const long MultipartOverheadBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var uploadOptions = app.ServiceProvider.GetRequiredService<IOptions<UploadOptions>>().Value;

        var group = app.MapGroup("/api/invoices").WithTags("Invoices");

        // JSON API called from our own frontend, not a cookie-authenticated form, so antiforgery is not needed.
        // Bodies well over the file limit are cut off by Kestrel with 413 before they are buffered.
        group.MapPost("/", Upload)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(uploadOptions.MaxFileSizeBytes + MultipartOverheadBytes));
        group.MapGet("/", List);
        group.MapGet("/suppliers", ListSuppliers);
        group.MapGet("/{id:guid}", GetById);
        group.MapGet("/{id:guid}/file", GetFile);
        group.MapPost("/{id:guid}/extract", Reextract);

        return app;
    }

    private static async Task<Results<Created<InvoiceResponse>, ValidationProblem>> Upload(
        IFormFile? file,
        InvoiceProcessorDbContext db,
        IFileStorage storage,
        IOptions<UploadOptions> uploadOptions,
        InvoiceProcessingQueue queue,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var options = uploadOptions.Value;

        if (file is null || file.Length == 0)
        {
            return FileError("A non-empty file is required.");
        }

        if (file.Length > options.MaxFileSizeBytes)
        {
            return FileError($"The file exceeds the {options.MaxFileSizeMb} MB limit.");
        }

        await using var content = file.OpenReadStream();

        var header = new byte[InvoiceFileType.HeaderLength];
        var headerLength = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var fileType = InvoiceFileType.Detect(header.AsSpan(0, headerLength));
        if (fileType is null)
        {
            var supported = string.Join(", ", InvoiceFileType.All.Select(t => t.Name));
            return FileError($"Unsupported file type. Supported types: {supported}.");
        }

        content.Position = 0;
        var path = await storage.SaveAsync(content, fileType.Extension, cancellationToken);

        var invoice = new Invoice
        {
            FileName = CleanFileName(file.FileName, fileType),
            FilePath = path,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        db.Invoices.Add(invoice);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        // Extraction runs in the background; the client polls the invoice until it leaves Processing.
        queue.Enqueue(invoice.Id);

        return TypedResults.Created($"/api/invoices/{invoice.Id}", InvoiceResponse.From(invoice));
    }

    // Runs extraction again, e.g. after a failure or a prompt or model change.
    private static async Task<Results<Accepted<InvoiceResponse>, NotFound, Conflict<ProblemDetails>>> Reextract(
        Guid id,
        InvoiceProcessorDbContext db,
        InvoiceProcessingQueue queue,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice is null)
        {
            return TypedResults.NotFound();
        }

        if (invoice.Status is not (InvoiceStatus.Failed or InvoiceStatus.PendingReview))
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Invoice cannot be re-extracted",
                Detail = $"Only failed invoices or invoices pending review can be re-extracted; this one is {invoice.Status}.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        invoice.Status = InvoiceStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);
        queue.Enqueue(invoice.Id);

        return TypedResults.Accepted($"/api/invoices/{invoice.Id}", InvoiceResponse.From(invoice));
    }

    // Dashboard list, newest first. Supplier matches as a substring (case-insensitive by column collation).
    private static async Task<Ok<InvoiceListResponse>> List(
        InvoiceProcessorDbContext db,
        CancellationToken cancellationToken,
        InvoiceStatus? status = null,
        string? supplier = null,
        int page = 1,
        int pageSize = 25)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Invoices.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(supplier))
        {
            var term = supplier.Trim();
            query = query.Where(i => i.Supplier != null && i.Supplier.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceSummary(
                i.Id, i.FileName, i.Status, i.Supplier, i.InvoiceNumber, i.Date, i.Currency, i.Total,
                i.ValidationIssues.Count, i.CreatedAt, i.ReviewedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new InvoiceListResponse(items, totalCount, page, pageSize));
    }

    // Distinct supplier names for the dashboard filter.
    private static async Task<Ok<List<string>>> ListSuppliers(InvoiceProcessorDbContext db, CancellationToken cancellationToken)
    {
        var suppliers = await db.Invoices
            .Where(i => i.Supplier != null)
            .Select(i => i.Supplier!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(suppliers);
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound>> GetById(
        Guid id,
        InvoiceProcessorDbContext db,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .Include(i => i.ValidationIssues)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice is null)
        {
            return TypedResults.NotFound();
        }

        // Usage for the reviewer, and for a failed invoice the reason it failed, next to the retry button.
        var lastExtraction = await db.ExtractionLogs.LatestForAsync(id, cancellationToken);

        return TypedResults.Ok(InvoiceResponse.From(invoice, lastExtraction));
    }

    private static async Task<Results<FileStreamHttpResult, NotFound>> GetFile(
        Guid id,
        InvoiceProcessorDbContext db,
        IFileStorage storage,
        CancellationToken cancellationToken)
    {
        var filePath = await db.Invoices
            .Where(i => i.Id == id)
            .Select(i => i.FilePath)
            .FirstOrDefaultAsync(cancellationToken);
        if (filePath is null)
        {
            return TypedResults.NotFound();
        }

        Stream stream;
        try
        {
            stream = await storage.OpenReadAsync(filePath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return TypedResults.NotFound();
        }

        // No download name, so the browser renders it inline for the review screen's viewer.
        var contentType = InvoiceFileType.FromPath(filePath)?.ContentType ?? "application/octet-stream";
        return TypedResults.Stream(stream, contentType, enableRangeProcessing: true);
    }

    private static ValidationProblem FileError(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });

    // Keep only the name part (clients may send a full path, with either separator) and cap its length.
    private static string CleanFileName(string fileName, InvoiceFileType fileType)
    {
        var name = fileName.Split('/', '\\')[^1].Trim();
        if (name.Length == 0)
        {
            return $"invoice{fileType.Extension}";
        }

        return name.Length <= InvoiceFieldLimits.FileName ? name : name[..InvoiceFieldLimits.FileName];
    }
}
