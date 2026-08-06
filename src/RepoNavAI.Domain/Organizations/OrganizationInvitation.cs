using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Organizations;

public sealed class OrganizationInvitation : Entity
{
    private OrganizationInvitation() { }

    public OrganizationInvitation(Guid organizationId, string email, OrganizationRole role, string tokenHash, Guid invitedByUserId, DateTimeOffset expiresAtUtc)
        : base(Guid.NewGuid())
    {
        if (role == OrganizationRole.Owner) throw new ArgumentException("Owner access cannot be granted through an invitation.", nameof(role));
        OrganizationId = organizationId;
        Email = string.IsNullOrWhiteSpace(email) ? throw new ArgumentException("Email is required.", nameof(email)) : email.Trim().ToLowerInvariant();
        Role = role;
        TokenHash = string.IsNullOrWhiteSpace(tokenHash) ? throw new ArgumentException("Token hash is required.", nameof(tokenHash)) : tokenHash;
        InvitedByUserId = invitedByUserId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public OrganizationRole Role { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid InvitedByUserId { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Organization Organization { get; private set; } = null!;
    public bool IsPending(DateTimeOffset now) => AcceptedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > now;

    public void Accept(DateTimeOffset now)
    {
        if (!IsPending(now)) throw new InvalidOperationException("Invitation is no longer valid.");
        AcceptedAtUtc = now;
        MarkUpdated();
    }

    public void Revoke(DateTimeOffset now)
    {
        if (AcceptedAtUtc is not null) throw new InvalidOperationException("An accepted invitation cannot be revoked.");
        if (RevokedAtUtc is not null) throw new InvalidOperationException("Invitation has already been revoked.");
        RevokedAtUtc = now;
        MarkUpdated();
    }
}
