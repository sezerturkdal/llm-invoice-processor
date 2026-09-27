using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace InvoiceProcessor.Api.Auth;

public static class AuthSetup
{
    public const string CookieName = "invoiceprocessor.auth";

    /// <summary>
    /// ASP.NET Core Identity with an HttpOnly cookie. The React app is served from the same origin as the API
    /// (Vite proxy in development, nginx in the container), so a cookie is safer than a token kept in JavaScript:
    /// script injected into the page cannot read it, and SameSite=Strict keeps other sites from sending it.
    /// </summary>
    public static IServiceCollection AddInvoiceProcessorAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var authSection = configuration.GetSection(AuthOptions.SectionName);
        services.Configure<AuthOptions>(authSection);
        var authOptions = authSection.Get<AuthOptions>() ?? new AuthOptions();

        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Length over character-class rules, as current guidance (NIST 800-63B) recommends.
                options.Password.RequiredLength = authOptions.PasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<InvoiceProcessorDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            // An API answers with status codes; the React app decides where to send the user.
            options.Events.OnRedirectToLogin = context => SetStatus(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => SetStatus(context, StatusCodes.Status403Forbidden);
        });

        // Role changes, deactivation and password resets update the security stamp; existing sessions
        // notice within this interval instead of keeping their old rights until the cookie expires.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddAuthorizationBuilder()
            // Secure by default: an endpoint is public only when it says AllowAnonymous.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Review, policy => policy.RequireRole(Roles.Reviewer, Roles.Admin))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin));

        services.AddScoped<IdentitySeeder>();

        return services;
    }

    private static Task SetStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
