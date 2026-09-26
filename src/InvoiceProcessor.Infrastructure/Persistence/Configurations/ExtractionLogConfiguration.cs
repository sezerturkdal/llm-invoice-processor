using InvoiceProcessor.Core.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceProcessor.Infrastructure.Persistence.Configurations;

public class ExtractionLogConfiguration : IEntityTypeConfiguration<ExtractionLog>
{
    public void Configure(EntityTypeBuilder<ExtractionLog> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Provider).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Model).HasMaxLength(128).IsRequired();
        builder.Property(e => e.CostEstimate).HasPrecision(18, 6);
        builder.Property(e => e.RawResponse); // nvarchar(max) by default
    }
}
