using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace InvoiceProcessor.Api.Auth;

/// <summary>
/// Creates the roles and the configured accounts at startup. Existing accounts are left alone,
/// so changing a seeded password in configuration does not overwrite one set in the app.
/// </summary>
public sealed class IdentitySeeder(
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
    IOptions<AuthOptions> options,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                Check(await roles.CreateAsync(new IdentityRole(role)), $"role {role}");
            }
        }

        var admin = options.Value.Admin;
        if (admin is { Email: not null, Password: not null })
        {
            await EnsureUserAsync(admin.Email, admin.Password, Roles.Admin);
        }

        foreach (var demo in options.Value.DemoUsers.Where(u => u is { Email: not null, Password: not null }))
        {
            await EnsureUserAsync(demo.Email!, demo.Password!, demo.Role);
        }

        if (!users.Users.Any())
        {
            logger.LogWarning("No users exist. Set Auth:Admin:Email and Auth:Admin:Password to create the first admin.");
        }
    }

    private async Task EnsureUserAsync(string email, string password, string role)
    {
        if (!Roles.All.Contains(role))
        {
            throw new InvalidOperationException($"Seed user {email} has unknown role '{role}'. Roles: {string.Join(", ", Roles.All)}.");
        }

        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        Check(await users.CreateAsync(user, password), $"user {email}");
        Check(await users.AddToRoleAsync(user, role), $"role for {email}");

        logger.LogInformation("Created {Role} account {Email}", role, email);
    }

    private static void Check(IdentityResult result, string what)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not create {what}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }
}
