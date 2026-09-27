using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Core.Invoices;

namespace InvoiceProcessor.Api.Tests;

[Collection(ApiCollection.Name)]
public class AuthorizationTests(ApiFactory api)
{
    [Theory]
    [InlineData("/api/invoices")]
    [InlineData("/api/invoices/suppliers")]
    [InlineData("/api/extractions/stats")]
    [InlineData("/api/users")]
    [InlineData("/api/auth/me")]
    public async Task Anonymous_requests_are_rejected(string path)
    {
        var response = await api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_check_is_public()
    {
        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/invoices", HttpStatusCode.OK)]
    [InlineData("/api/extractions/stats", HttpStatusCode.Forbidden)]
    [InlineData("/api/users", HttpStatusCode.Forbidden)]
    public async Task Reviewer_can_review_but_not_see_costs_or_users(string path, HttpStatusCode expected)
    {
        var client = await api.SignedInClientAsync(ApiFactory.ReviewerEmail);

        var response = await client.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/invoices")]
    [InlineData("/api/extractions/stats")]
    [InlineData("/api/users")]
    public async Task Admin_can_reach_everything(string path)
    {
        var client = await api.SignedInClientAsync(ApiFactory.AdminEmail);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Session_cookie_is_http_only_and_same_site_strict()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.ReviewerEmail, password = ApiFactory.Password });

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("invoiceprocessor.auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ApiFactory.ReviewerEmail, "wrong-password")]
    [InlineData("nobody@test.local", ApiFactory.Password)]
    public async Task Failed_sign_in_does_not_reveal_whether_the_account_exists(string email, string password)
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Sign_out_ends_the_session()
    {
        var client = await api.SignedInClientAsync(ApiFactory.ReviewerEmail);

        await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Disabling_a_user_blocks_sign_in_and_ends_their_session()
    {
        var admin = await api.SignedInClientAsync(ApiFactory.AdminEmail);
        var (id, email) = await CreateUserAsync(admin, "Reviewer");
        var user = await api.SignedInClientAsync(email);

        var disable = await admin.PutAsJsonAsync($"/api/users/{id}", new { role = "Reviewer", isActive = false });

        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/invoices")).StatusCode);
        var signIn = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, signIn.StatusCode);
    }

    [Fact]
    public async Task Promotion_to_admin_takes_effect_in_the_existing_session()
    {
        var admin = await api.SignedInClientAsync(ApiFactory.AdminEmail);
        var (id, email) = await CreateUserAsync(admin, "Reviewer");
        var user = await api.SignedInClientAsync(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/users")).StatusCode);

        await admin.PutAsJsonAsync($"/api/users/{id}", new { role = "Admin", isActive = true });

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_demote_or_disable_themselves()
    {
        var admin = await api.SignedInClientAsync(ApiFactory.AdminEmail);
        var users = await admin.GetFromJsonAsync<JsonElement>("/api/users");
        var self = users.EnumerateArray().Single(u => u.GetProperty("email").GetString() == ApiFactory.AdminEmail).GetProperty("id").GetString();

        var demote = await admin.PutAsJsonAsync($"/api/users/{self}", new { role = "Reviewer", isActive = true });
        var disable = await admin.PutAsJsonAsync($"/api/users/{self}", new { role = "Admin", isActive = false });

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, disable.StatusCode);
    }

    [Fact]
    public async Task Approving_records_who_reviewed_the_invoice()
    {
        var invoiceId = await api.WithDbAsync(async db =>
        {
            var invoice = new Invoice { FileName = "a.pdf", FilePath = "2026/09/a.pdf", CreatedAt = DateTimeOffset.UtcNow };
            invoice.ApplyExtraction(
                new ExtractedInvoice("Acme Ltd", $"INV-{Guid.NewGuid():N}", new DateOnly(2026, 9, 1), "EUR", [new ExtractedInvoiceLine("Work", 1, 100, 100)], 100, 20, 120),
                "test-model");
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            return invoice.Id;
        });
        var reviewer = await api.SignedInClientAsync(ApiFactory.ReviewerEmail);

        var response = await reviewer.PostAsync($"/api/invoices/{invoiceId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.Equal(ApiFactory.ReviewerEmail, body.GetProperty("reviewedBy").GetString());
    }

    private static async Task<(string Id, string Email)> CreateUserAsync(HttpClient admin, string role)
    {
        var email = $"user-{Guid.NewGuid():N}@test.local";
        var response = await admin.PostAsJsonAsync("/api/users", new { email, role, password = ApiFactory.Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetString()!, email);
    }
}
