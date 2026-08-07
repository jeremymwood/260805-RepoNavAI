using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Application.Repositories;

public sealed record GitHubRepositoryAddress(string Owner, string Name)
{
    public static bool TryParse(string? value, out GitHubRepositoryAddress? address)
    {
        address = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2) return false;
        var name = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
        if (string.IsNullOrWhiteSpace(segments[0]) || string.IsNullOrWhiteSpace(name)) return false;
        address = new GitHubRepositoryAddress(segments[0].ToLowerInvariant(), name.ToLowerInvariant());
        return true;
    }
}

public sealed record ProviderRepository(string ProviderRepositoryId, string Owner, string Name, string DefaultBranch, RepositoryVisibility Visibility, string WebUrl);
public sealed record RepositoryDto(Guid Id, Guid OrganizationId, string Owner, string Name, string FullName, string DefaultBranch, RepositoryVisibility Visibility, string WebUrl, IndexingRequestStatus IndexingStatus, DateTimeOffset RegisteredAtUtc);

public interface IRepositoryProvider
{
    Task<ProviderRepository?> GetAsync(GitHubRepositoryAddress address, CancellationToken cancellationToken);
}

public interface IRepositoryRegistrationRepository
{
    Task<bool> ExistsAsync(Guid organizationId, string owner, string name, CancellationToken cancellationToken);
    Task AddAsync(RegisteredRepository repository, RepositoryIndexingRequest indexingRequest, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IRepositoryQueries
{
    Task<IReadOnlyCollection<RepositoryDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
}
