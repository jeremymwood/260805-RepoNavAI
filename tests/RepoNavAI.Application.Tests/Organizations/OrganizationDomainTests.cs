using FluentAssertions;
using RepoNavAI.Domain.Organizations;
using Xunit;

namespace RepoNavAI.Application.Tests.Organizations;

public sealed class OrganizationDomainTests
{
    [Fact]
    public void RemoveMember_WhenRemovingLastOwner_IsRejected()
    {
        var ownerId = Guid.NewGuid();
        var organization = new Organization("Acme", "acme");
        organization.AddMember(ownerId, OrganizationRole.Owner);
        var action = () => organization.RemoveMember(ownerId);
        action.Should().Throw<InvalidOperationException>().WithMessage("*last organization owner*");
    }

    [Fact]
    public void ChangeMemberRole_WhenDemotingLastOwner_IsRejected()
    {
        var ownerId = Guid.NewGuid();
        var organization = new Organization("Acme", "acme");
        organization.AddMember(ownerId, OrganizationRole.Owner);
        var action = () => organization.ChangeMemberRole(ownerId, OrganizationRole.Member);
        action.Should().Throw<InvalidOperationException>().WithMessage("*at least one owner*");
    }

    [Fact]
    public void ChangeMemberRole_WhenAnotherOwnerExists_Succeeds()
    {
        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        var organization = new Organization("Acme", "acme");
        organization.AddMember(firstOwner, OrganizationRole.Owner);
        organization.AddMember(secondOwner, OrganizationRole.Owner);
        organization.ChangeMemberRole(firstOwner, OrganizationRole.Administrator);
        organization.Members.Single(x => x.UserId == firstOwner).Role.Should().Be(OrganizationRole.Administrator);
    }
}
