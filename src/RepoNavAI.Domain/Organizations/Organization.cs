using RepoNavAI.Domain.Common;

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

    private static string Require(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameter) : value.Trim();
}
