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
                repository.IndexingRequests.OrderByDescending(request => request.CreatedAtUtc).Select(request => request.Status).First(),
                repository.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
}
