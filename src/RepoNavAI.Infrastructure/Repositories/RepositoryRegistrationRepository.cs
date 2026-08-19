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

public sealed class RepositoryRemovalStore(AppDbContext dbContext) : IRepositoryRemovalStore
{
    public async Task RemoveAsync(Guid organizationId, Guid repositoryId, Guid actorUserId, string confirmation, DateTimeOffset removedAtUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var repository = await dbContext.RegisteredRepositories
            .FromSqlInterpolated($"SELECT * FROM reponav.\"RegisteredRepositories\" WHERE \"OrganizationId\" = {organizationId} AND \"Id\" = {repositoryId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Repository was not found.");

        if (!string.Equals(repository.FullName, confirmation, StringComparison.OrdinalIgnoreCase))
            throw new FluentValidation.ValidationException($"Enter {repository.FullName} to confirm repository removal.");

        await dbContext.RepositoryIndexingRequests
            .Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId && (x.Status == IndexingRequestStatus.Pending || x.Status == IndexingRequestStatus.Processing))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CancellationRequestedAtUtc, removedAtUtc), cancellationToken);
        await dbContext.RepositoryRemovalAudits.AddAsync(new RepositoryRemovalAudit(organizationId, repositoryId, actorUserId, repository.Provider, repository.Owner, repository.Name, removedAtUtc), cancellationToken);
        dbContext.RegisteredRepositories.Remove(repository);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed class RepositoryQueries(AppDbContext dbContext) : IRepositoryQueries
{
    public Task<bool> ExistsAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) => dbContext.RegisteredRepositories.AnyAsync(x => x.OrganizationId == organizationId && x.Id == repositoryId, cancellationToken);
    public async Task<RepositoryPage> ListAsync(Guid organizationId, Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.RegisteredRepositories.AsNoTracking().Where(repository => repository.OrganizationId == organizationId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(repository => dbContext.RepositoryFavorites.Any(favorite => favorite.OrganizationId == organizationId && favorite.UserId == userId && favorite.RepositoryId == repository.Id))
            .ThenBy(repository => repository.Owner).ThenBy(repository => repository.Name).ThenBy(repository => repository.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
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
                repository.IndexingRequests.OrderByDescending(request => request.CreatedAtUtc).Select(request => request.ErrorMessage).First(),
                dbContext.RepositoryFavorites.Any(favorite => favorite.OrganizationId == organizationId && favorite.UserId == userId && favorite.RepositoryId == repository.Id)))
            .ToArrayAsync(cancellationToken);
        return new RepositoryPage(items, page, pageSize, totalCount);
    }

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

    public async Task<RepositoryCapabilitiesDto> GetCapabilitiesAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(organizationId, repositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
        var snapshot = await dbContext.RepositorySnapshots.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId).OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.CoverageStatus, x.CoverageJson }).FirstOrDefaultAsync(cancellationToken);
        if (snapshot is null) return new(false, false, false, false, false, []);
        var documents = await dbContext.RepositoryDocuments.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.SnapshotId == snapshot.Id).OrderBy(x => x.Path).Select(x => new { x.Path, x.Language }).ToArrayAsync(cancellationToken);
        var hasChunks = await dbContext.RepositoryChunks.AsNoTracking().AnyAsync(x => x.OrganizationId == organizationId && x.SnapshotId == snapshot.Id, cancellationToken);
        var hasEndpoints = await dbContext.RepositoryEndpoints.AsNoTracking().AnyAsync(x => x.OrganizationId == organizationId && x.SnapshotId == snapshot.Id, cancellationToken);
        var representativePaths = documents.Where(item => SourceLanguageRegistry.IsExecutableLanguage(item.Language)).Select(item => item.Path).Concat(documents.Where(item => item.Language == "markdown").Select(item => item.Path)).Take(5).ToArray();
        var languages = System.Text.Json.JsonSerializer.Deserialize<RepositoryLanguageCoverage[]>(snapshot.CoverageJson) ?? [];
        return new(hasChunks, documents.Any(item => SourceLanguageRegistry.IsExecutableLanguage(item.Language)), documents.Any(item => item.Path.Contains("test", StringComparison.OrdinalIgnoreCase)), documents.Any(item => item.Language == "markdown"), hasEndpoints, representativePaths, snapshot.CoverageStatus, languages);
    }

    public async Task<RepositoryArchitectureGraphDto> GetArchitectureAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken)
    {
        var repository = await dbContext.RegisteredRepositories.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == repositoryId)
            .Select(x => new { x.WebUrl }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Repository was not found.");
        var snapshot = await dbContext.RepositorySnapshots.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId)
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.CommitSha }).FirstOrDefaultAsync(cancellationToken);
        if (snapshot is null) return new("1.1", string.Empty, false, 0, [], []);

        const int fileLimit = 180;
        var allFiles = await dbContext.RepositoryDocuments.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.SnapshotId == snapshot.Id)
            .OrderBy(x => x.Path).Select(x => new { x.Id, x.Path, x.Language }).ToArrayAsync(cancellationToken);
        var files = allFiles.Take(fileLimit).ToArray();
        var moduleNames = files.Select(x => x.Path.Contains('/') ? x.Path[..x.Path.IndexOf('/')] : "root").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        var nodes = new List<RepositoryArchitectureNode>();
        var edges = new List<RepositoryArchitectureEdge>();
        foreach (var module in moduleNames)
        {
            var moduleFiles = files.Where(x => string.Equals(x.Path.Contains('/') ? x.Path[..x.Path.IndexOf('/')] : "root", module, StringComparison.OrdinalIgnoreCase)).ToArray();
            nodes.Add(new($"module:{module}", module, "Module", null, null, moduleFiles.Length, null, "Module"));
            foreach (var file in moduleFiles)
            {
                var fileId = $"file:{file.Id:N}";
                var sourceUrl = $"{repository.WebUrl}/blob/{snapshot.CommitSha}/{file.Path}";
                nodes.Add(new(fileId, file.Path.Split('/').Last(), "File", file.Path, file.Language, 0, sourceUrl, ClassifyArchitectureRole(file.Path)));
                edges.Add(new($"contains:{file.Id:N}", $"module:{module}", fileId, "Contains", "contains", "Confirmed", sourceUrl));
            }
        }

        var endpoints = await dbContext.RepositoryEndpoints.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.SnapshotId == snapshot.Id).OrderBy(x => x.Route).Take(40)
            .Select(x => new { x.Id, x.HttpMethod, x.Route, x.Path, x.Line }).ToArrayAsync(cancellationToken);
        foreach (var endpoint in endpoints)
        {
            var endpointId = $"endpoint:{endpoint.Id:N}";
            var sourceUrl = $"{repository.WebUrl}/blob/{snapshot.CommitSha}/{endpoint.Path}#L{endpoint.Line}";
            nodes.Add(new(endpointId, $"{endpoint.HttpMethod} {endpoint.Route}", "Endpoint", endpoint.Path, null, 0, sourceUrl, "Endpoint"));
            var file = files.FirstOrDefault(x => string.Equals(x.Path, endpoint.Path, StringComparison.Ordinal));
            if (file is not null) edges.Add(new($"declares:{endpoint.Id:N}", $"file:{file.Id:N}", endpointId, "Declares", "declares", "Confirmed", sourceUrl));
        }
        var totalNodeCount = allFiles.Length + moduleNames.Length + endpoints.Length;
        return new("1.1", snapshot.CommitSha, allFiles.Length > fileLimit, totalNodeCount, nodes, edges);
    }

    private static string ClassifyArchitectureRole(string path)
    {
        var value = path.ToLowerInvariant();
        if (value.Contains("controller") || value.Contains("endpoint")) return "Controller";
        if (value.Contains("handler")) return "Handler";
        if (value.Contains("service")) return "Service";
        if (value.Contains("interface") || value.Contains("contract")) return "Interface";
        if (value.Contains("repository") || value.Contains("persistence")) return "Repository";
        if (value.Contains("worker") || value.Contains("background") || value.Contains("job")) return "Background";
        if (value.Contains("client") || value.Contains("provider") || value.Contains("external")) return "External";
        if (value.Contains("database") || value.Contains("dbcontext") || value.Contains("store")) return "Data store";
        return "Component";
    }
}

public sealed class RepositoryFavoriteStore(AppDbContext dbContext) : IRepositoryFavoriteStore
{
    public async Task SetAsync(Guid organizationId, Guid repositoryId, Guid userId, bool isFavorite, CancellationToken cancellationToken)
    {
        var existing = await dbContext.RepositoryFavorites.SingleOrDefaultAsync(
            favorite => favorite.OrganizationId == organizationId && favorite.RepositoryId == repositoryId && favorite.UserId == userId,
            cancellationToken);
        if (isFavorite && existing is null) await dbContext.RepositoryFavorites.AddAsync(new RepositoryFavorite(organizationId, repositoryId, userId), cancellationToken);
        if (!isFavorite && existing is not null) dbContext.RepositoryFavorites.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
