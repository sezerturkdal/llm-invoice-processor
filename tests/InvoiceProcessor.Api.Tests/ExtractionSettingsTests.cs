using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceProcessor.Core.Extraction;
using InvoiceProcessor.Infrastructure.Extraction;
using InvoiceProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceProcessor.Api.Tests;

[Collection(ApiCollection.Name)]
public class ExtractionSettingsTests(ApiFactory api)
{
    [Fact]
    public async Task Lists_priced_claude_models_and_reports_ollama_not_running()
    {
        var admin = await api.SignedInClientAsync(ApiFactory.AdminEmail);

        var settings = await admin.GetFromJsonAsync<JsonElement>("/api/settings/extraction");

        var anthropic = Provider(settings, "Anthropic");
        Assert.True(anthropic.GetProperty("isSelectable").GetBoolean());
        Assert.Contains(anthropic.GetProperty("models").EnumerateArray(), m => m.GetProperty("id").GetString() == "claude-sonnet-5");

        var ollama = Provider(settings, "Ollama");
        Assert.False(ollama.GetProperty("isSelectable").GetBoolean());
        Assert.Contains("not reachable", ollama.GetProperty("unavailable").GetString());
    }

    [Fact]
    public async Task Choosing_a_model_applies_to_the_next_extraction_and_is_recorded()
    {
        var admin = await api.SignedInClientAsync(ApiFactory.AdminEmail);

        try
        {
            var response = await admin.PutAsJsonAsync("/api/settings/extraction", new { provider = "Anthropic", model = "claude-sonnet-5" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var selected = body.GetProperty("selected");
            Assert.Equal("claude-sonnet-5", selected.GetProperty("model").GetString());
            Assert.False(selected.GetProperty("isDefault").GetBoolean());
            Assert.Equal(ApiFactory.AdminEmail, selected.GetProperty("changedBy").GetString());
            Assert.Equal("claude-sonnet-5", body.GetProperty("history")[0].GetProperty("model").GetString());

            // What the extraction pipeline gets from now on.
            using var scope = api.Services.CreateScope();
            var extractor = await scope.ServiceProvider.GetRequiredService<ExtractionModelSelector>().GetExtractorAsync(CancellationToken.None);
            Assert.Equal("Anthropic", extractor.Provider);
            Assert.Equal("claude-sonnet-5", extractor.Model);
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/settings/extraction", new { provider = "Anthropic", model = "claude-opus-5" });
        }
    }

    [Theory]
    [InlineData("Anthropic", "claude-not-a-model", "not one of the available")]
    [InlineData("Ollama", "qwen3-vl:8b", "not reachable")]
    [InlineData("OpenAI", "gpt-5", "Unknown provider")]
    public async Task Unavailable_choices_are_refused(string provider, string model, string expected)
    {
        var admin = await api.SignedInClientAsync(ApiFactory.AdminEmail);

        var response = await admin.PutAsJsonAsync("/api/settings/extraction", new { provider, model });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reviewer_cannot_change_the_model()
    {
        var reviewer = await api.SignedInClientAsync(ApiFactory.ReviewerEmail);

        var response = await reviewer.PutAsJsonAsync("/api/settings/extraction", new { provider = "Anthropic", model = "claude-sonnet-5" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Configuration_that_allows_only_local_models_overrides_any_choice_in_the_app()
    {
        // The same database, served by an instance configured to never send documents to a cloud API.
        await using var localOnly = api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Llm:AllowedProviders:0", "Ollama");
            builder.UseSetting("Llm:Provider", "Ollama");
            builder.UseSetting("Llm:Model", "qwen3-vl:8b");
        });

        // An earlier choice of Claude, e.g. made before the configuration was tightened.
        using (var scope = localOnly.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InvoiceProcessorDbContext>();
            db.ExtractionModelChanges.Add(new ExtractionModelChange
            {
                Provider = "Anthropic",
                Model = "claude-opus-5",
                ChangedBy = ApiFactory.AdminEmail,
                ChangedAt = DateTimeOffset.UtcNow.AddYears(1),
            });
            await db.SaveChangesAsync();

            var extractor = await scope.ServiceProvider.GetRequiredService<ExtractionModelSelector>().GetExtractorAsync(CancellationToken.None);
            Assert.Equal("Ollama", extractor.Provider);

            try
            {
                var client = localOnly.CreateClient();
                (await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.Password })).EnsureSuccessStatusCode();

                var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings/extraction");
                Assert.False(Provider(settings, "Anthropic").GetProperty("allowed").GetBoolean());
                Assert.True(settings.GetProperty("selected").GetProperty("isDefault").GetBoolean());

                var response = await client.PutAsJsonAsync("/api/settings/extraction", new { provider = "Anthropic", model = "claude-opus-5" });
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("not allowed", await response.Content.ReadAsStringAsync());
            }
            finally
            {
                // Leave the shared database as the other tests expect it.
                db.ExtractionModelChanges.RemoveRange(db.ExtractionModelChanges.Where(c => c.ChangedAt > DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }
        }
    }

    private static JsonElement Provider(JsonElement settings, string name) =>
        settings.GetProperty("providers").EnumerateArray().Single(p => p.GetProperty("provider").GetString() == name);
}
