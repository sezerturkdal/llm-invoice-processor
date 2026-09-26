using InvoiceAi.Core.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceAi.Infrastructure.Persistence.Configurations;

public class ValidationIssueConfiguration : IEntityTypeConfiguration<ValidationIssue>
{
    public void Configure(EntityTypeBuilder<ValidationIssue> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Field).HasMaxLength(128).IsRequired();
        builder.Property(v => v.Rule).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Message).HasMaxLength(1024).IsRequired();
    }
}
