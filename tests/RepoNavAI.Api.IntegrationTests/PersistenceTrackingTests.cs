using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Infrastructure.Persistence;
using RepoNavAI.Infrastructure.Repositories;
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

    [Fact]
    public void RepositoryChatModel_IndexesOrganizationUsageWindowWithoutPersistingPromptsOrAnswers()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options;
        using var dbContext = new AppDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(RepoNavAI.Domain.Repositories.RepositoryChatSession));
        entity!.GetIndexes().Should().Contain(index => index.Properties.Select(property => property.Name).SequenceEqual(new[] { "OrganizationId", "CreatedAtUtc" }));
        entity.FindProperty("Question").Should().BeNull();
        entity.FindProperty("Answer").Should().BeNull();
    }

    [Fact]
    public void RepositoryFavoriteModel_EnforcesPerUserRepositoryUniqueness()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options;
        using var dbContext = new AppDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(RepoNavAI.Domain.Repositories.RepositoryFavorite));
        entity!.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "OrganizationId", "UserId", "RepositoryId" }));
    }

    [Fact]
    public void RepositoryRemovalAudit_IsMetadataOnlyAndSurvivesRepositoryDeletion()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options;
        using var dbContext = new AppDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(RepoNavAI.Domain.Repositories.RepositoryRemovalAudit));

        entity.Should().NotBeNull();
        entity!.GetForeignKeys().Should().BeEmpty();
        entity.FindProperty("WebUrl").Should().BeNull();
        entity.FindProperty("SourceContent").Should().BeNull();
        entity.GetIndexes().Should().Contain(index => index.Properties.Select(property => property.Name).SequenceEqual(new[] { "OrganizationId", "RemovedAtUtc" }));
    }

    [Fact]
    public void RepositoryOwnedData_CascadesFromRegisteredRepository()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options;
        using var dbContext = new AppDbContext(options);
        var repositoryType = dbContext.Model.FindEntityType(typeof(RepoNavAI.Domain.Repositories.RegisteredRepository));
        var dependents = dbContext.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys())
            .Where(key => key.PrincipalEntityType == repositoryType)
            .ToDictionary(key => key.DeclaringEntityType.ClrType.Name, key => key.DeleteBehavior);

        foreach (var dependent in new[] { "RepositoryFavorite", "RepositoryIndexingRequest", "RepositorySnapshot", "RepositoryChatSession", "RepositoryOrientationPlan", "RepositoryAssistantHistory" })
            dependents[dependent].Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void AssistantHistoryModel_CascadesWithUserAndDoesNotStoreProviderPayloadFields()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=tracking-test", postgres => postgres.UseVector()).Options;
        using var dbContext = new AppDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(RepoNavAI.Domain.Repositories.RepositoryAssistantHistory));

        entity.Should().NotBeNull();
        entity!.FindProperty("ProviderRequest").Should().BeNull(); entity.FindProperty("SystemPrompt").Should().BeNull(); entity.FindProperty("ProviderDiagnostics").Should().BeNull();
        entity.GetForeignKeys().Single(key => key.Properties.Single().Name == "UserId").DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void AssistantHistoryPolicy_EnforcesRetentionAndStorageBoundaries()
    {
        var now = DateTimeOffset.UtcNow;

        RepositoryAssistantHistoryPolicy.RetentionCutoff(now, 90).Should().Be(now.AddDays(-90));
        RepositoryAssistantHistoryPolicy.ResultFits("é", 2).Should().BeTrue();
        RepositoryAssistantHistoryPolicy.ResultFits("é", 1).Should().BeFalse();
        RepositoryAssistantHistoryPolicy.OrganizationHasRoom(8, "ab", 10).Should().BeTrue();
        RepositoryAssistantHistoryPolicy.OrganizationHasRoom(9, "ab", 10).Should().BeFalse();
    }
}
