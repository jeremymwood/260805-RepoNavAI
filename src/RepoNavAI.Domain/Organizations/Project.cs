using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Organizations;

public sealed class Project : Entity
{
    private Project() { }
    public Project(Guid organizationId, string name, string? description = null) : base(Guid.NewGuid())
    {
        OrganizationId = organizationId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Project name is required.", nameof(name)) : name.Trim();
        Description = description?.Trim();
    }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Organization Organization { get; private set; } = null!;
}
