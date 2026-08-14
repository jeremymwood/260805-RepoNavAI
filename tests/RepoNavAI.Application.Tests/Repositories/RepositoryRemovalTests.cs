using FluentAssertions;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Domain.Repositories;
using Xunit;

namespace RepoNavAI.Application.Tests.Repositories;

public sealed class RepositoryRemovalTests
{
    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Administrator)]
    public async Task Remove_WithAdministrativeRole_ForwardsExactConfirmationAndAuditIdentity(OrganizationRole role)
    {
        var userId = Guid.NewGuid(); var organization = OrganizationWith(userId, role); var store = new RemovalStore();
        var now = new DateTimeOffset(2026, 8, 14, 1, 2, 3, TimeSpan.Zero);
        var handler = new RemoveRepositoryHandler(new OrganizationAccess(new OrganizationStore(organization)), store, new CurrentUser(userId), new FixedTimeProvider(now));
        var repositoryId = Guid.NewGuid();

        await handler.Handle(new RemoveRepositoryCommand(organization.Id, repositoryId, "acme/platform"), CancellationToken.None);

        store.Request.Should().Be((organization.Id, repositoryId, userId, "acme/platform", now));
    }

    [Fact]
    public async Task Remove_AsMember_IsForbiddenBeforeStoreMutation()
    {
        var userId = Guid.NewGuid(); var organization = OrganizationWith(userId, OrganizationRole.Member); var store = new RemovalStore();
        var handler = new RemoveRepositoryHandler(new OrganizationAccess(new OrganizationStore(organization)), store, new CurrentUser(userId), TimeProvider.System);

        var action = () => handler.Handle(new RemoveRepositoryCommand(organization.Id, Guid.NewGuid(), "acme/platform"), CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenException>();
        store.Request.Should().BeNull();
    }

    private static Organization OrganizationWith(Guid userId, OrganizationRole role) { var organization = new Organization("Acme", Guid.NewGuid().ToString("N")); organization.AddMember(userId, role); return organization; }
    private sealed class RemovalStore : IRepositoryRemovalStore
    {
        public (Guid OrganizationId, Guid RepositoryId, Guid ActorUserId, string Confirmation, DateTimeOffset RemovedAtUtc)? Request { get; private set; }
        public Task RemoveAsync(Guid organizationId, Guid repositoryId, Guid actorUserId, string confirmation, DateTimeOffset removedAtUtc, CancellationToken cancellationToken) { Request = (organizationId, repositoryId, actorUserId, confirmation, removedAtUtc); return Task.CompletedTask; }
    }
    private sealed class CurrentUser(Guid id) : ICurrentUser { public Guid UserId => id; public string Email => "user@example.com"; public bool IsAuthenticated => true; }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class OrganizationStore(Organization organization) : IOrganizationRepository
    {
        public Task<Organization?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == organization.Id ? organization : null);
        public Task AddAsync(Organization value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<OrganizationInvitation?> GetInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<bool> HasPendingInvitationAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> IsEmailMemberAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
