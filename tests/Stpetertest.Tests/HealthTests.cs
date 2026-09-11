using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Stpetertest.Tests;

// Hosts the real application in memory rather than testing a stub, so the
// coverage the pipeline measures reflects code that actually runs.
public class HealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task Root_ReportsTheServiceName()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<HealthResponse>("/");

        Assert.NotNull(body);
        Assert.Equal(ServiceInfo.Name, body!.Service);
    }
}
