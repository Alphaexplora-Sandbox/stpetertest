using Stpetertest.Domain;

var builder = WebApplication.CreateBuilder(args);

// Registered as interfaces so a test can replace them without replacing the
// host. Concrete registrations would leave nothing to substitute.
builder.Services.AddSingleton<IServiceStatus, ServiceStatus>();
builder.Services.AddSingleton<INoteStore, InMemoryNoteStore>();

// Resolved from the container rather than read off builder.Configuration, so
// the credential comes from whatever configuration the running host ended up
// with — including the one a test supplies.
//
// There is no default credential, deliberately. This service is deployed to a
// public URL, so a fallback would be the same fallback on every environment.
// Absent configuration means tokens cannot be issued at all.
builder.Services.AddSingleton<ITokenIssuer>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    return new TokenIssuer(configuration["AUTH_USERNAME"], configuration["AUTH_PASSWORD"]);
});

// Describes the API so the contract tests have something to generate from.
// Document only — Swagger UI is a separate package and is not referenced, so
// nothing new is published by the running service.
builder.Services.AddOpenApi();

var app = builder.Build();

// The production gate probes /health after a deployment, so this endpoint is
// part of the pipeline contract rather than a convenience.
// .Produces<T>() is what puts a response SHAPE in the document. Without it the
// schema says an endpoint exists and nothing about what it returns, and a
// contract test generated from that can only check the status code — it would
// pass against an endpoint returning anything.
app.MapGet("/health", (IServiceStatus status) =>
    Results.Ok(new HealthResponse(status.CurrentStatus(), ServiceInfo.Name)))
   .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapGet("/", () => Results.Ok(new HealthResponse("ready", ServiceInfo.Name)))
   .Produces<HealthResponse>(StatusCodes.Status200OK);

// Exchange a credential for a token. The API suite calls this first and carries
// the result through the rest of the run, which is the dynamic auth flow the
// verify stage exists to exercise.
app.MapPost("/auth/token", (TokenRequest request, ITokenIssuer issuer) =>
{
    if (!issuer.IsConfigured)
    {
        // 503 rather than 401: nothing the caller sent is wrong. The service
        // has no credential configured and cannot answer at all. Declared in
        // the document below so a contract test treats it as a known answer
        // rather than an undocumented one.
        return Results.Problem(
            detail: "Set AUTH_USERNAME and AUTH_PASSWORD to enable authentication.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var token = issuer.Issue(request.Username, request.Password);
    return token is null
        ? Results.Unauthorized()
        : Results.Ok(new TokenResponse(token));
})
   .Produces<TokenResponse>(StatusCodes.Status200OK)
   .Produces(StatusCodes.Status401Unauthorized)
   .Produces(StatusCodes.Status503ServiceUnavailable);

// Notes: the smallest resource that makes a CRUD regression suite mean
// something. Every route below requires a token.
var notes = app.MapGroup("/notes")
    .AddEndpointFilter(async (context, next) =>
    {
        var issuer = context.HttpContext.RequestServices.GetRequiredService<ITokenIssuer>();
        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..]
            : null;

        return issuer.IsValid(token)
            ? await next(context)
            : Results.Unauthorized();
    });

notes.MapGet("/", (INoteStore store) => Results.Ok(store.All()))
   .Produces<IReadOnlyCollection<Note>>(StatusCodes.Status200OK)
   .Produces(StatusCodes.Status401Unauthorized);

notes.MapGet("/{id:guid}", (Guid id, INoteStore store) =>
    store.Find(id) is { } note ? Results.Ok(note) : Results.NotFound())
   .Produces<Note>(StatusCodes.Status200OK)
   .Produces(StatusCodes.Status404NotFound)
   .Produces(StatusCodes.Status401Unauthorized);

notes.MapPost("/", (NoteRequest request, INoteStore store) =>
{
    // Validated rather than trusted: a contract fuzzer sends an empty title on
    // its first pass, and an unguarded store would accept it — which would make
    // the schema's promise about Title a lie.
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = ["Title is required."],
        });
    }

    var created = store.Add(request.Title, request.Body ?? string.Empty);
    return Results.Created($"/notes/{created.Id}", created);
})
   .Produces<Note>(StatusCodes.Status201Created)
   .ProducesValidationProblem()
   .Produces(StatusCodes.Status401Unauthorized);

notes.MapPut("/{id:guid}", (Guid id, NoteRequest request, INoteStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = ["Title is required."],
        });
    }

    return store.Replace(id, request.Title, request.Body ?? string.Empty) is { } updated
        ? Results.Ok(updated)
        : Results.NotFound();
})
   .Produces<Note>(StatusCodes.Status200OK)
   .Produces(StatusCodes.Status404NotFound)
   .ProducesValidationProblem()
   .Produces(StatusCodes.Status401Unauthorized);

notes.MapDelete("/{id:guid}", (Guid id, INoteStore store) =>
    store.Remove(id) ? Results.NoContent() : Results.NotFound())
   .Produces(StatusCodes.Status204NoContent)
   .Produces(StatusCodes.Status404NotFound)
   .Produces(StatusCodes.Status401Unauthorized);

app.Run();

public record HealthResponse(string Status, string Service);

public record TokenRequest(string Username, string Password);

public record TokenResponse(string Token);

public record NoteRequest(string? Title, string? Body);

public static class ServiceInfo
{
    public const string Name = "stpetertest";
}

// Exposed so the test project can host the application in memory.
public partial class Program;
