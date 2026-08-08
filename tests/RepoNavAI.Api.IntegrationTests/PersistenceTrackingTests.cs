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
            .UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector())
            .Options;
        using var dbContext = new AppDbContext(options);
        var organization = new Organization("Acme", "acme");
        organization.AddMember(Guid.NewGuid(), OrganizationRole.Owner);
        dbContext.Attach(organization);

        var member = organization.AddMember(Guid.NewGuid(), OrganizationRole.Member);
        dbContext.ChangeTracker.DetectChanges();

        dbContext.Entry(member).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public void IndexingModel_EnforcesOneSnapshotPerRepositoryCommit()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options;
        using var dbContext = new AppDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(RepoNavAI.Domain.Repositories.RepositorySnapshot));
        entity!.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "RepositoryId", "CommitSha" }));
    }

    [Fact]
    public void EndpointModel_EnforcesOneRouteHandlerPerSnapshot()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options;
        using var dbContext = new AppDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(RepoNavAI.Domain.Repositories.RepositoryEndpoint));
        entity!.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "SnapshotId", "HttpMethod", "Route", "Handler" }));
    }

    [Fact]
    public void SemanticModel_UsesPgvectorAndIdempotentChunkIdentity()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options;
        using var dbContext = new AppDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(RepoNavAI.Domain.Repositories.RepositoryChunk));
        entity!.FindProperty("Embedding")!.GetColumnType().Should().Be("vector(512)");
        entity.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "SnapshotId", "DocumentId", "Ordinal" }));
    }
}
