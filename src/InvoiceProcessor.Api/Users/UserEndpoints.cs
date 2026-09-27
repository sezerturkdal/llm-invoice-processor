using System.Security.Claims;
using InvoiceProcessor.Api.Auth;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceProcessor.Api.Users;

/// <summary>User management for admins: create accounts, change roles, disable, reset passwords.</summary>
public static class UserEndpoints
{
    // Disabling an account is an Identity lockout that never ends; failed-attempt lockouts end within minutes.
    private static readonly DateTimeOffset DisabledUntil = new(9999, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization(Policies.Admin);

        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapPut("/{id}", Update);
        group.MapPost("/{id}/password", ResetPassword);

        return app;
    }

    private static async Task<Ok<List<UserResponse>>> List(InvoiceProcessorDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new UserResponse(
                u.Id,
                u.Email!,
                db.UserRoles.Where(ur => ur.UserId == u.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name).FirstOrDefault(),
                u.LockoutEnd == null || u.LockoutEnd < DisabledUntil))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(users);
    }

    private static async Task<Results<Created<UserResponse>, ValidationProblem>> Create(
        CreateUserRequest request,
        UserManager<IdentityUser> users)
    {
        var errors = new Dictionary<string, string[]>();
        CheckRole(request.Role, errors);
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors["password"] = ["Password is required."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var email = request.Email!.Trim();
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };

        var created = await users.CreateAsync(user, request.Password!);
        if (!created.Succeeded)
        {
            return TypedResults.ValidationProblem(ToErrors(created));
        }

        await users.AddToRoleAsync(user, request.Role!);

        return TypedResults.Created($"/api/users/{user.Id}", new UserResponse(user.Id, email, request.Role, IsActive: true));
    }

    private static async Task<Results<Ok<UserResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> Update(
        string id,
        UpdateUserRequest request,
        ClaimsPrincipal currentUser,
        UserManager<IdentityUser> users)
    {
        var errors = new Dictionary<string, string[]>();
        CheckRole(request.Role, errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var currentRole = (await users.GetRolesAsync(user)).FirstOrDefault();
        var isActive = user.LockoutEnd is null || user.LockoutEnd < DisabledUntil;

        // An admin cannot demote or disable themselves, so there is always at least one active admin.
        if (user.Id == users.GetUserId(currentUser) && (request.Role != currentRole || !request.IsActive))
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Cannot change your own account",
                Detail = "Ask another admin to change your role or disable your account.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        if (request.Role != currentRole)
        {
            if (currentRole is not null)
            {
                await users.RemoveFromRoleAsync(user, currentRole);
            }

            await users.AddToRoleAsync(user, request.Role!);
        }

        if (request.IsActive != isActive)
        {
            await users.SetLockoutEnabledAsync(user, true);
            await users.SetLockoutEndDateAsync(user, request.IsActive ? null : DisabledUntil);
            await users.ResetAccessFailedCountAsync(user);

            // Signs the user out everywhere at the next security stamp check.
            await users.UpdateSecurityStampAsync(user);
        }

        return TypedResults.Ok(new UserResponse(user.Id, user.Email!, request.Role, request.IsActive));
    }

    // An admin sets a new password directly: there is no email delivery in this app.
    private static async Task<Results<NoContent, NotFound, ValidationProblem>> ResetPassword(
        string id,
        ResetPasswordRequest request,
        UserManager<IdentityUser> users)
    {
        if (string.IsNullOrEmpty(request.Password))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["Password is required."] });
        }

        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var reset = await users.ResetPasswordAsync(user, token, request.Password);

        return reset.Succeeded ? TypedResults.NoContent() : TypedResults.ValidationProblem(ToErrors(reset));
    }

    private static void CheckRole(string? role, Dictionary<string, string[]> errors)
    {
        if (role is null || !Roles.All.Contains(role))
        {
            errors["role"] = [$"Role must be one of: {string.Join(", ", Roles.All)}."];
        }
    }

    // Identity error codes mapped to the form field they belong to. The user name is the email,
    // so its duplicate error would only repeat the email one.
    private static Dictionary<string, string[]> ToErrors(IdentityResult result) =>
        result.Errors
            .Where(e => e.Code != nameof(IdentityErrorDescriber.DuplicateUserName))
            .GroupBy(e => e.Code switch
            {
                _ when e.Code.StartsWith("Password", StringComparison.Ordinal) => "password",
                _ when e.Code.Contains("Email", StringComparison.Ordinal) || e.Code.Contains("UserName", StringComparison.Ordinal) => "email",
                _ => "",
            })
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}

public sealed record UserResponse(string Id, string Email, string? Role, bool IsActive);

public sealed record CreateUserRequest(string? Email, string? Role, string? Password);

public sealed record UpdateUserRequest(string? Role, bool IsActive);

public sealed record ResetPasswordRequest(string? Password);
