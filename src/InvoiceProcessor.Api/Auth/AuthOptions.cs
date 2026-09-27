namespace InvoiceProcessor.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Minimum password length. Only Development lowers it, so the demo accounts can use short passwords.</summary>
    public int PasswordMinLength { get; set; } = 10;

    /// <summary>
    /// The first admin, created at startup if no user has this email yet. Set it through
    /// Auth__Admin__Email / Auth__Admin__Password (.env) or user-secrets, never in appsettings.
    /// </summary>
    public SeedUser? Admin { get; set; }

    /// <summary>Known accounts for local development and demos; only appsettings.Development.json sets these.</summary>
    public List<SeedUser> DemoUsers { get; set; } = [];
}

public sealed class SeedUser
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string Role { get; set; } = Roles.Admin;
}
