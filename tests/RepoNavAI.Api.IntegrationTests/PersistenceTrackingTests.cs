using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Infrastructure.Persistence;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class PersistenceTrackingTests
{
    [Fact]
    public void AddingMemberToTrackedOrganization_TracksMemberAsAdded()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=tracking-test")
            .Options;
        using var dbContext = new AppDbContext(options);
        var organization = new Organization("Acme", "acme");
        organization.AddMember(Guid.NewGuid(), OrganizationRole.Owner);
        dbContext.Attach(organization);

        var member = organization.AddMember(Guid.NewGuid(), OrganizationRole.Member);
        dbContext.ChangeTracker.DetectChanges();

        dbContext.Entry(member).State.Should().Be(EntityState.Added);
    }
}
