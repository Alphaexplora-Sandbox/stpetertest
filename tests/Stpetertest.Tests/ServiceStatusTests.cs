using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Stpetertest.Domain;
using Xunit;

namespace Stpetertest.Tests;

public class ServiceStatusTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServiceStatusTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Reports_What_The_Domain_Says()
    {
        // The substitute stands in for the real IServiceStatus, so this
        // asserts the endpoint asks the domain rather than answering "ok"
        // from a literal. A hard-coded endpoint would pass the other test
        // and fail this one.
        var status = Substitute.For<IServiceStatus>();
        status.CurrentStatus().Returns("degraded");

        // ConfigureTestServices runs AFTER the application registers its
        // own services, and the last registration wins — which is what
        // makes the substitute take effect without touching Program.cs.
        var client = _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                    services.AddSingleton(status)))
            .CreateClient();

        var body = await client.GetFromJsonAsync<HealthResponse>("/health");

        Assert.NotNull(body);
        Assert.Equal("degraded", body!.Status);
        status.Received(1).CurrentStatus();
    }
}
