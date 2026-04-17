using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

var keycloak = builder.Configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>()
    ?? throw new InvalidOperationException("Missing Keycloak configuration.");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IPartnerRepository, InMemoryPartnerRepository>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloak.Authority;
        options.Audience = keycloak.ClientId;
        options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/partners", (IPartnerRepository repository) =>
{
    var partners = repository.GetAll();
    return Results.Ok(partners);
}).RequireAuthorization();

app.MapGet("/partners/{id:guid}", (Guid id, IPartnerRepository repository) =>
{
    var partner = repository.GetById(id);
    return partner is null ? Results.NotFound() : Results.Ok(partner);
}).RequireAuthorization();

app.MapPost("/partners", (CreatePartnerRequest request, IPartnerRepository repository) =>
{
    var partner = repository.Create(request);
    return Results.Created($"/partners/{partner.Id}", partner);
}).RequireAuthorization();

app.MapPut("/partners/{id:guid}", (Guid id, UpdatePartnerRequest request, IPartnerRepository repository) =>
{
    var updated = repository.Update(id, request);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
}).RequireAuthorization();

app.MapDelete("/partners/{id:guid}", (Guid id, IPartnerRepository repository) =>
{
    var deleted = repository.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.Run();

public record Partner(Guid Id, string Name, string Email, string Status, DateTimeOffset CreatedAtUtc);

public record CreatePartnerRequest(string Name, string Email, string Status);

public record UpdatePartnerRequest(string Name, string Email, string Status);

public interface IPartnerRepository
{
    IReadOnlyCollection<Partner> GetAll();
    Partner? GetById(Guid id);
    Partner Create(CreatePartnerRequest request);
    Partner? Update(Guid id, UpdatePartnerRequest request);
    bool Delete(Guid id);
}

public sealed class InMemoryPartnerRepository : IPartnerRepository
{
    private readonly ConcurrentDictionary<Guid, Partner> _partners = new();

    public IReadOnlyCollection<Partner> GetAll() => _partners.Values.OrderBy(p => p.Name).ToArray();

    public Partner? GetById(Guid id)
    {
        return _partners.GetValueOrDefault(id);
    }

    public Partner Create(CreatePartnerRequest request)
    {
        var partner = new Partner(
            Guid.NewGuid(),
            request.Name.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            request.Status.Trim(),
            DateTimeOffset.UtcNow);

        _partners[partner.Id] = partner;
        return partner;
    }

    public Partner? Update(Guid id, UpdatePartnerRequest request)
    {
        var existing = GetById(id);
        if (existing is null)
        {
            return null;
        }

        var updated = existing with
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Status = request.Status.Trim(),
        };

        _partners[id] = updated;
        return updated;
    }

    public bool Delete(Guid id)
    {
        return _partners.TryRemove(id, out _);
    }
}

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string Authority { get; init; } = string.Empty;
    public string Realm { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public bool RequireHttpsMetadata { get; init; } = true;
}
