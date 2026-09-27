using InvoiceProcessor.Core.Invoices;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Infrastructure.Persistence;

// Users and roles live in the same database as the invoices (ASP.NET Core Identity tables).
public class InvoiceProcessorDbContext(DbContextOptions<InvoiceProcessorDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<ValidationIssue> ValidationIssues => Set<ValidationIssue>();
    public DbSet<ExtractionLog> ExtractionLogs => Set<ExtractionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Plain names instead of Identity's default "AspNet" prefix, matching the rest of the schema.
        modelBuilder.Entity<IdentityUser>().ToTable("Users");
        modelBuilder.Entity<IdentityRole>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoiceProcessorDbContext).Assembly);
    }
}
