using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceProcessor.Infrastructure.Persistence.Configurations;

public class ExtractionModelChangeConfiguration : IEntityTypeConfiguration<ExtractionModelChange>
{
    public void Configure(EntityTypeBuilder<ExtractionModelChange> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Provider).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Model).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ChangedBy).HasMaxLength(InvoiceFieldLimits.ReviewedBy).IsRequired();

        builder.HasIndex(e => e.ChangedAt);
    }
}
