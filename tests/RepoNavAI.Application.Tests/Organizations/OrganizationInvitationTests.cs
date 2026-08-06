using FluentAssertions;
using RepoNavAI.Domain.Organizations;
using Xunit;

namespace RepoNavAI.Application.Tests.Organizations;

public sealed class OrganizationInvitationTests
{
    [Fact]
    public void Revoke_MakesInvitationNoLongerPending()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new OrganizationInvitation(Guid.NewGuid(), "developer@example.com", OrganizationRole.Member, new string('a', 64), Guid.NewGuid(), now.AddDays(7));

        invitation.Revoke(now);

        invitation.IsPending(now).Should().BeFalse();
        invitation.RevokedAtUtc.Should().Be(now);
    }
}
