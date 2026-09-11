using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Stpetertest.Tests;

// The same rules as NoteStoreTests, driven through HTTP. A store test proves
// the rule; this proves the rule is reachable on a route, behind the auth
// filter, returning the status codes the OpenAPI document promises. The
// contract tests in the verify stage assert those codes against the deployed
// service, so a mismatch here is a mismatch there.
public class NotesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string User = "ci";
    private const string Password = "test-secret";

    private readonly WebApplicationFactory<Program> _factory;

    public NotesEndpointTests(WebApplicationFactory<Program> factory)
    {
        // Supplied here rather than baked into the app: the credential is
        // configuration everywhere, tests included.
        _factory = WithCredential(factory, User, Password);
    }

    [Fact]
    public async Task Notes_AreRejected_WithoutAToken()
    {
        // The whole resource sits behind the filter. If this ever passes, every
        // test below is measuring nothing.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Notes_AreRejected_ForAFabricatedToken()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "made-up");

        var response = await client.GetAsync("/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_IsRefused_ForAWrongPassword()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/token",
            new { username = User, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Notes_CanBeCreatedReadUpdatedAndDeleted()
    {
        var client = await AuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync(
            "/notes",
            new { title = "Release checklist", body = "Check the gates" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var note = await created.Content.ReadFromJsonAsync<NotePayload>();
        Assert.NotNull(note);

        var fetched = await client.GetAsync($"/notes/{note!.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var updated = await client.PutAsJsonAsync(
            $"/notes/{note.Id}",
            new { title = "Release checklist v2", body = "Check the gates twice" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var reread = await updated.Content.ReadFromJsonAsync<NotePayload>();
        Assert.Equal("Release checklist v2", reread!.Title);
        Assert.Equal(note.Id, reread.Id);

        var deleted = await client.DeleteAsync($"/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var gone = await client.GetAsync($"/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public async Task Notes_AreRefused_WithoutATitle(string title)
    {
        // The first thing a contract fuzzer sends. An unguarded store would
        // accept it, and the schema's promise about Title would be a lie.
        var client = await AuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/notes", new { title, body = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Notes_ReportNotFound_ForANoteThatWasNeverThere()
    {
        var client = await AuthenticatedClientAsync();
        var missing = Guid.NewGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/notes/{missing}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/notes/{missing}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(
                $"/notes/{missing}",
                new { title = "Ghost", body = string.Empty })).StatusCode);
    }

    [Fact]
    public async Task Token_IsUnavailable_WhenNoCredentialIsConfigured()
    {
        // A deployment with no credential set must say so, not hand out a token
        // that opens the resource to everyone.
        using var bare = WithCredential(new WebApplicationFactory<Program>(), null, null);
        var client = bare.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/token",
            new { username = "anyone", password = "anything" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // In-memory configuration is added last, so it wins over any AUTH_USERNAME
    // the developer happens to have exported. Without this the credential tests
    // would pass or fail depending on the machine they ran on.
    private static WebApplicationFactory<Program> WithCredential(
        WebApplicationFactory<Program> factory,
        string? username,
        string? password) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AUTH_USERNAME"] = username,
                    ["AUTH_PASSWORD"] = password,
                })));

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/token",
            new { username = User, password = Password });
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenPayload>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!.Token);
        return client;
    }

    private sealed record TokenPayload(string Token);

    private sealed record NotePayload(Guid Id, string Title, string Body);
}
