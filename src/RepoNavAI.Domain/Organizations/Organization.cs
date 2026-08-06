using RepoNavAI.Domain.Common;
using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Domain.Organizations;

public sealed class Organization : Entity
{
    private Organization() { }

    public Organization(string name, string slug) : base(Guid.NewGuid())
    {
        Name = Require(name, nameof(name));
        Slug = Require(slug, nameof(slug)).ToLowerInvariant();
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public ICollection<OrganizationMember> Members { get; private set; } = new List<OrganizationMember>();
    public ICollection<Project> Projects { get; private set; } = new List<Project>();
    public ICollection<RegisteredRepository> Repositories { get; private set; } = new List<RegisteredRepository>();

    public void Rename(string name)
    {
        Name = Require(name, nameof(name));
        MarkUpdated();
    }

    public OrganizationMember AddMember(Guid userId, OrganizationRole role)
    {
        if (Members.Any(x => x.UserId == userId)) throw new InvalidOperationException("User is already an organization member.");
        var member = new OrganizationMember(Id, userId, role);
        Members.Add(member);
        MarkUpdated();
        return member;
    }

    public void ChangeMemberRole(Guid userId, OrganizationRole role)
    {
        var member = Members.SingleOrDefault(x => x.UserId == userId) ?? throw new InvalidOperationException("Organization member was not found.");
        if (member.Role == OrganizationRole.Owner && role != OrganizationRole.Owner && Members.Count(x => x.Role == OrganizationRole.Owner) == 1)
            throw new InvalidOperationException("An organization must have at least one owner.");
        member.ChangeRole(role);
        MarkUpdated();
    }

    public void RemoveMember(Guid userId)
    {
        var member = Members.SingleOrDefault(x => x.UserId == userId) ?? throw new InvalidOperationException("Organization member was not found.");
        if (member.Role == OrganizationRole.Owner && Members.Count(x => x.Role == OrganizationRole.Owner) == 1)
            throw new InvalidOperationException("The last organization owner cannot be removed.");
        Members.Remove(member);
        MarkUpdated();
    }

    private static string Require(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameter) : value.Trim();
}
