using Microsoft.EntityFrameworkCore;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Persistence;
using RepoNavAI.Application.Common.Exceptions;
using Npgsql;

namespace RepoNavAI.Infrastructure.Repositories;

public sealed class RepositoryRegistrationRepository(AppDbContext dbContext) : IRepositoryRegistrationRepository
{
    public Task<bool> ExistsAsync(Guid organizationId, string owner, string name, CancellationToken cancellationToken) =>
        dbContext.RegisteredRepositories.AnyAsync(x => x.OrganizationId == organizationId && x.Provider == RepositoryProvider.GitHub && x.Owner == owner && x.Name == name, cancellationToken);

    public async Task AddAsync(RegisteredRepository repository, RepositoryIndexingRequest indexingRequest, CancellationToken cancellationToken)
    {
        await dbContext.RegisteredRepositories.AddAsync(repository, cancellationToken);
        await dbContext.RepositoryIndexingRequests.AddAsync(indexingRequest, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Repository is already registered in this organization.");
        }
    }
}

public sealed class RepositoryQueries(AppDbContext dbContext) : IRepositoryQueries
{
    public async Task<IReadOnlyCollection<RepositoryDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await dbContext.RegisteredRepositories.AsNoTracking()
            .Where(repository => repository.OrganizationId == organizationId)
            .OrderBy(repository => repository.Owner).ThenBy(repository => repository.Name)
            .Select(repository => new RepositoryDto(
                repository.Id,
                repository.OrganizationId,
                repository.Owner,
                repository.Name,
                repository.Owner + "/" + repository.Name,
                repository.DefaultBranch,
                repository.Visibility,
                repository.WebUrl,
                repository.IndexingRequests.OrderByDescending(request => request.CreatedAtUtc).Select(request => request.Status).First(), repository.CreatedAtUtc,
                repository.IndexingRequests.OrderByDescending(request => request.CreatedAtUtc).Select(request => request.Checkpoint).First(),
                repository.IndexingRequests.OrderByDescending(request => request.CreatedAtUtc).Select(request => request.CommitSha).First(),
                repository.IndexingRequests.OrderByDescending(request => request.CreatedAtUtc).Select(request => request.ErrorMessage).First()))
            .ToArrayAsync(cancellationToken);

    public async Task<IndexingRequestDto?> GetIndexingRequestAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) =>
        await dbContext.RepositoryIndexingRequests.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId)
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => new IndexingRequestDto(x.Id, x.RepositoryId, x.Status, x.Checkpoint, x.AttemptCount, x.CommitSha, x.ErrorCode, x.ErrorMessage, x.CreatedAtUtc, x.CompletedAtUtc)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RepositoryEndpointDto>> ListEndpointsAsync(Guid organizationId, Guid repositoryId, string? method, string? search, bool? requiresAuthorization, CancellationToken cancellationToken)
    {
        var repository = await dbContext.RegisteredRepositories.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.Id == repositoryId).Select(x => new { x.WebUrl }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Repository was not found.");
        var snapshotId = await dbContext.RepositorySnapshots.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId).OrderByDescending(x => x.CreatedAtUtc).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (snapshotId is null) return [];
        var query = dbContext.RepositoryEndpoints.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.SnapshotId == snapshotId);
        if (!string.IsNullOrWhiteSpace(method)) { var normalized = method.Trim().ToUpperInvariant(); query = query.Where(x => x.HttpMethod == normalized); }
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => EF.Functions.ILike(x.Route, $"%{term}%") || EF.Functions.ILike(x.Handler, $"%{term}%")); }
        if (requiresAuthorization.HasValue) query = query.Where(x => x.RequiresAuthorization == requiresAuthorization.Value);
        var rows = await query.OrderBy(x => x.Route).ThenBy(x => x.HttpMethod).Select(x => new { x.Id, x.HttpMethod, x.Route, x.Handler, x.Path, x.Line, x.RequiresAuthorization, x.DownstreamSymbols, x.Snapshot.CommitSha }).ToArrayAsync(cancellationToken);
        return rows.Select(x => new RepositoryEndpointDto(x.Id, x.HttpMethod, x.Route, x.Handler, x.Path, x.Line, x.RequiresAuthorization, string.IsNullOrWhiteSpace(x.DownstreamSymbols) ? [] : System.Text.Json.JsonSerializer.Deserialize<string[]>(x.DownstreamSymbols) ?? [], x.CommitSha, $"{repository.WebUrl}/blob/{x.CommitSha}/{x.Path}#L{x.Line}")).ToArray();
    }
}
