using InvoiceAi.Core.Invoices;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAi.Infrastructure.Persistence;

public class InvoiceAiDbContext(DbContextOptions<InvoiceAiDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<ValidationIssue> ValidationIssues => Set<ValidationIssue>();
    public DbSet<ExtractionLog> ExtractionLogs => Set<ExtractionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoiceAiDbContext).Assembly);
    }
}
