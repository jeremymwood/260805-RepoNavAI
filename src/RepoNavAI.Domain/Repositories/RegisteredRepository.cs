using RepoNavAI.Domain.Common;
using RepoNavAI.Domain.Organizations;

namespace RepoNavAI.Domain.Repositories;

public sealed class RegisteredRepository : Entity
{
    private RegisteredRepository() { }

    public RegisteredRepository(Guid organizationId, string providerRepositoryId, string owner, string name, string defaultBranch, RepositoryVisibility visibility, string webUrl, Guid registeredByUserId)
        : base(Guid.NewGuid())
    {
        OrganizationId = organizationId;
        ProviderRepositoryId = Require(providerRepositoryId, nameof(providerRepositoryId));
        Owner = Require(owner, nameof(owner)).ToLowerInvariant();
        Name = Require(name, nameof(name)).ToLowerInvariant();
        DefaultBranch = Require(defaultBranch, nameof(defaultBranch));
        Visibility = visibility;
        WebUrl = Require(webUrl, nameof(webUrl));
        RegisteredByUserId = registeredByUserId;
    }

    public Guid OrganizationId { get; private set; }
    public RepositoryProvider Provider { get; private set; } = RepositoryProvider.GitHub;
    public string ProviderRepositoryId { get; private set; } = string.Empty;
    public string Owner { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string FullName => $"{Owner}/{Name}";
    public string DefaultBranch { get; private set; } = string.Empty;
    public RepositoryVisibility Visibility { get; private set; }
    public string WebUrl { get; private set; } = string.Empty;
    public Guid RegisteredByUserId { get; private set; }
    public Organization Organization { get; private set; } = null!;
    public ICollection<RepositoryIndexingRequest> IndexingRequests { get; private set; } = new List<RepositoryIndexingRequest>();
    public ICollection<RepositorySnapshot> Snapshots { get; private set; } = new List<RepositorySnapshot>();

    private static string Require(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameter) : value.Trim();
}

public enum RepositoryProvider { GitHub = 1 }
public enum RepositoryVisibility { Public = 1, Private = 2 }
