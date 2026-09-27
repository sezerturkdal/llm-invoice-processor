using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace InvoiceProcessor.Api.Auth;

/// <summary>
/// Sign-in and sign-out for the React app. Accounts are created by an admin (see UserEndpoints),
/// so there is deliberately no self-registration or password-reset-by-email flow.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", Login).AllowAnonymous();
        group.MapPost("/logout", Logout).AllowAnonymous();
        group.MapGet("/me", Me);

        return app;
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> Login(
        LoginRequest request,
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) ? null : await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return InvalidCredentials();
        }

        // Counts failures, so repeated guessing locks the account for a while.
        var result = await signIn.PasswordSignInAsync(user, request.Password ?? "", isPersistent: false, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return TypedResults.Problem(
                title: "Sign-in blocked",
                detail: "This account is disabled or temporarily locked after too many failed attempts.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        var role = (await users.GetRolesAsync(user)).FirstOrDefault();
        return TypedResults.Ok(new CurrentUserResponse(user.Email!, role));
    }

    private static async Task<NoContent> Logout(SignInManager<IdentityUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    // Who is signed in; 401 (from the fallback policy) when nobody is.
    private static Ok<CurrentUserResponse> Me(ClaimsPrincipal user) =>
        TypedResults.Ok(new CurrentUserResponse(user.Identity!.Name!, Roles.All.FirstOrDefault(user.IsInRole)));

    // One message for an unknown email and a wrong password, so sign-in does not reveal which accounts exist.
    private static ProblemHttpResult InvalidCredentials() =>
        TypedResults.Problem(title: "Sign-in failed", detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record LoginRequest(string? Email, string? Password);

public sealed record CurrentUserResponse(string Email, string? Role);
