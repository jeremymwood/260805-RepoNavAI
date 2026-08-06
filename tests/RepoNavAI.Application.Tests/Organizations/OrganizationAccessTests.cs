using FluentAssertions;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;
using Xunit;

namespace RepoNavAI.Application.Tests.Organizations;

public sealed class OrganizationAccessTests
{
    [Fact]
    public async Task RequireAsync_WhenUserIsNotMember_ReturnsNotFoundToAvoidTenantDisclosure()
    {
        var organization = CreateOrganizationWithOwner(Guid.NewGuid());
        var access = new OrganizationAccess(new StubRepository(organization));
        var action = () => access.RequireAsync(organization.Id, Guid.NewGuid(), OrganizationRole.Member, CancellationToken.None);
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RequireAsync_WhenMemberRequestsAdministratorAccess_IsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var organization = CreateOrganizationWithOwner(ownerId);
        organization.AddMember(memberId, OrganizationRole.Member);
        var access = new OrganizationAccess(new StubRepository(organization));
        var action = () => access.RequireAsync(organization.Id, memberId, OrganizationRole.Administrator, CancellationToken.None);
        await action.Should().ThrowAsync<ForbiddenException>();
    }

    [Theory]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Administrator)]
    [InlineData(OrganizationRole.Administrator, OrganizationRole.Member)]
    [InlineData(OrganizationRole.Member, OrganizationRole.Member)]
    public async Task RequireAsync_WithSufficientRole_ReturnsOrganization(OrganizationRole actual, OrganizationRole required)
    {
        var userId = Guid.NewGuid();
        var organization = new Organization("Acme", "acme");
        organization.AddMember(userId, actual);
        var result = await new OrganizationAccess(new StubRepository(organization)).RequireAsync(organization.Id, userId, required, CancellationToken.None);
        result.Should().BeSameAs(organization);
    }

    private static Organization CreateOrganizationWithOwner(Guid ownerId)
    {
        var organization = new Organization("Acme", "acme");
        organization.AddMember(ownerId, OrganizationRole.Owner);
        return organization;
    }

    private sealed class StubRepository(Organization organization) : IOrganizationRepository
    {
        public Task<Organization?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == organization.Id ? organization : null);
        public Task AddAsync(Organization value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<bool> HasPendingInvitationAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> IsEmailMemberAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
