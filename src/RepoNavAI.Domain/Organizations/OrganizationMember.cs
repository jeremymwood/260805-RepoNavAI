using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Organizations;

public sealed class OrganizationMember : Entity
{
    private OrganizationMember() { }

    public OrganizationMember(Guid organizationId, Guid userId, OrganizationRole role) : base(Guid.NewGuid())
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
    }

    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public OrganizationRole Role { get; private set; }
    public Organization Organization { get; private set; } = null!;

    internal void ChangeRole(OrganizationRole role)
    {
        Role = role;
        MarkUpdated();
    }
}

public enum OrganizationRole { Owner = 1, Administrator = 2, Member = 3 }
