namespace Partner.Api.Tests;

public class PartnerRepositoryTests
{
    [Fact]
    public void Create_ShouldNormalizeEmailAndStorePartner()
    {
        var repository = new InMemoryPartnerRepository();

        var created = repository.Create(new CreatePartnerRequest("Acme", " SALES@ACME.COM ", "Active"));

        Assert.Equal("sales@acme.com", created.Email);
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void Delete_ShouldRemovePartner()
    {
        var repository = new InMemoryPartnerRepository();
        var created = repository.Create(new CreatePartnerRequest("Acme", "sales@acme.com", "Active"));

        var removed = repository.Delete(created.Id);

        Assert.True(removed);
        Assert.Empty(repository.GetAll());
    }
}
