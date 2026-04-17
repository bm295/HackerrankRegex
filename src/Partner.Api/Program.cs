using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IPartnerRepository, InMemoryPartnerRepository>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/partners", (IPartnerRepository repository) =>
{
    var partners = repository.GetAll();
    return Results.Ok(partners);
});

app.MapGet("/partners/{id:guid}", (Guid id, IPartnerRepository repository) =>
{
    var partner = repository.GetById(id);
    return partner is null ? Results.NotFound() : Results.Ok(partner);
});

app.MapPost("/partners", (CreatePartnerRequest request, IPartnerRepository repository) =>
{
    var partner = repository.Create(request);
    return Results.Created($"/partners/{partner.Id}", partner);
});

app.MapPut("/partners/{id:guid}", (Guid id, UpdatePartnerRequest request, IPartnerRepository repository) =>
{
    var updated = repository.Update(id, request);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapDelete("/partners/{id:guid}", (Guid id, IPartnerRepository repository) =>
{
    var deleted = repository.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

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
