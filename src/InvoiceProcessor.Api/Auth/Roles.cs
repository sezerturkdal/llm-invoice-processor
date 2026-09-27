namespace InvoiceProcessor.Api.Auth;

/// <summary>Every user has exactly one role.</summary>
public static class Roles
{
    /// <summary>Uploads, corrects, approves and rejects invoices.</summary>
    public const string Reviewer = "Reviewer";

    /// <summary>Everything a reviewer does, plus usage and cost figures and user management.</summary>
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Admin, Reviewer];
}

public static class Policies
{
    public const string Review = "Review";
    public const string Admin = "Admin";
}
