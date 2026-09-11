using Stpetertest.Domain;

var builder = WebApplication.CreateBuilder(args);

// Registered as an interface so a test can replace it without replacing
// the host. A concrete registration would leave nothing to substitute.
builder.Services.AddSingleton<IServiceStatus, ServiceStatus>();

// Describes the API so the contract tests have something to generate
// from. Document only — Swagger UI is a separate package and is not
// referenced, so nothing new is published by the running service.
builder.Services.AddOpenApi();

var app = builder.Build();

// The production gate probes /health after a deployment, so this endpoint
// is part of the pipeline contract rather than a convenience.
// .Produces<T>() is what puts a response SHAPE in the document. Without
// it the schema says an endpoint exists and nothing about what it
// returns, and a contract test generated from that can only check the
// status code — it would pass against an endpoint returning anything.
app.MapGet("/health", (IServiceStatus status) =>
    Results.Ok(new HealthResponse(status.CurrentStatus(), ServiceInfo.Name)))
   .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapGet("/", () => Results.Ok(new HealthResponse("ready", ServiceInfo.Name)))
   .Produces<HealthResponse>(StatusCodes.Status200OK);

app.Run();

public record HealthResponse(string Status, string Service);

public static class ServiceInfo
{
    public const string Name = "stpetertest";
}

// Exposed so the test project can host the application in memory.
public partial class Program;
