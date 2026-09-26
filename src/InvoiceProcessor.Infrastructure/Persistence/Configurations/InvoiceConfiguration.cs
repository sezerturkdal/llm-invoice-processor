using InvoiceProcessor.Core.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceProcessor.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.FileName).HasMaxLength(260).IsRequired();
        builder.Property(i => i.FilePath).HasMaxLength(1024).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(i => i.Supplier).HasMaxLength(256);
        builder.Property(i => i.InvoiceNumber).HasMaxLength(128);
        builder.Property(i => i.Currency).HasMaxLength(8);
        builder.Property(i => i.Net).HasPrecision(18, 2);
        builder.Property(i => i.Vat).HasPrecision(18, 2);
        builder.Property(i => i.Total).HasPrecision(18, 2);
        builder.Property(i => i.ModelUsed).HasMaxLength(128);

        builder.HasMany(i => i.Lines)
            .WithOne()
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.ValidationIssues)
            .WithOne()
            .HasForeignKey(v => v.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.ExtractionLogs)
            .WithOne()
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Not unique: a duplicate is flagged by validation for review, not rejected by the database.
        builder.HasIndex(i => new { i.Supplier, i.InvoiceNumber });
        builder.HasIndex(i => i.Status);
    }
}
