using InvoiceProcessor.Api.Validation;
using InvoiceProcessor.Core.Invoices;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Api.Invoices;

/// <summary>The human-in-the-loop step: correct the extracted data, then approve or reject.</summary>
public static class InvoiceReviewEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices").WithTags("Review");

        group.MapPut("/{id:guid}", Update);
        group.MapPost("/{id:guid}/approve", Approve);
        group.MapPost("/{id:guid}/reject", Reject);

        return app;
    }

    // Saves the reviewer's corrections and re-runs validation, so flags clear as fields are fixed.
    private static async Task<Results<Ok<InvoiceResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> Update(
        Guid id,
        InvoiceUpdateRequest request,
        InvoiceProcessorDbContext db,
        InvoiceValidationService validation,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var invoice = await LoadForReviewAsync(db, id, cancellationToken);
        if (invoice is null)
        {
            return TypedResults.NotFound();
        }

        if (!invoice.CanBeEdited)
        {
            return WrongState(invoice, "corrected");
        }

        invoice.ApplyCorrections(request.ToDetails());
        await validation.ValidateAsync(invoice, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(InvoiceResponse.From(invoice));
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound, Conflict<ProblemDetails>>> Approve(
        Guid id,
        InvoiceProcessorDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var invoice = await LoadForReviewAsync(db, id, cancellationToken);
        if (invoice is null)
        {
            return TypedResults.NotFound();
        }

        if (!invoice.CanBeEdited)
        {
            return WrongState(invoice, "approved");
        }

        if (invoice.HasBlockingIssues)
        {
            var missing = invoice.ValidationIssues.Where(i => i.Rule == Core.Validation.ValidationRules.RequiredFields).Select(i => i.Field);
            return Conflict("Invoice is incomplete", $"Fill in the missing fields before approving: {string.Join(", ", missing)}.");
        }

        invoice.Approve(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(InvoiceResponse.From(invoice));
    }

    private static async Task<Results<Ok<InvoiceResponse>, NotFound, Conflict<ProblemDetails>>> Reject(
        Guid id,
        InvoiceProcessorDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var invoice = await LoadForReviewAsync(db, id, cancellationToken);
        if (invoice is null)
        {
            return TypedResults.NotFound();
        }

        if (!invoice.CanBeRejected)
        {
            return WrongState(invoice, "rejected");
        }

        invoice.Reject(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(InvoiceResponse.From(invoice));
    }

    private static Task<Invoice?> LoadForReviewAsync(InvoiceProcessorDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.ValidationIssues)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    private static Conflict<ProblemDetails> WrongState(Invoice invoice, string action) =>
        Conflict($"Invoice cannot be {action}", $"The invoice is {invoice.Status}.");

    private static Conflict<ProblemDetails> Conflict(string title, string detail) =>
        TypedResults.Conflict(new ProblemDetails { Title = title, Detail = detail, Status = StatusCodes.Status409Conflict });
}
