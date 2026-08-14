using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Persistence;

namespace RepoNavAI.Infrastructure.Repositories;

public sealed class RepositoryAssistantHistoryOptions
{
    public const string SectionName = "AssistantHistory";
    public int RetentionDays { get; init; } = 90;
    public int MaximumEntriesPerUserRepository { get; init; } = 200;
    public int MaximumResultBytes { get; init; } = 262_144;
    public int MaximumOrganizationStoredCharacters { get; init; } = 25_000_000;
}

public static class RepositoryAssistantHistoryPolicy
{
    public static DateTimeOffset RetentionCutoff(DateTimeOffset now, int retentionDays) => now.AddDays(-retentionDays);
    public static bool ResultFits(string? resultJson, int maximumBytes) => resultJson is null || Encoding.UTF8.GetByteCount(resultJson) <= maximumBytes;
    public static bool OrganizationHasRoom(long storedCharacters, string? resultJson, int maximumCharacters) => storedCharacters + (resultJson?.Length ?? 0) <= maximumCharacters;
}

public sealed class RepositoryAssistantHistoryStore(AppDbContext db, TimeProvider timeProvider,
    IOptions<RepositoryAssistantHistoryOptions> options) : IRepositoryAssistantHistoryStore
{
    public async Task<RepositoryAssistantHistory> StartAsync(Guid organizationId, Guid repositoryId, Guid userId,
        RepositoryAssistantHistoryMode mode, string prompt, string commitSha, CancellationToken cancellationToken)
    {
        await PruneExpiredAsync(cancellationToken);
        await MakeRoomAsync(organizationId, repositoryId, userId, cancellationToken);
        var entry = new RepositoryAssistantHistory(organizationId, repositoryId, userId, mode, prompt.Trim(), commitSha, timeProvider.GetUtcNow());
        await db.RepositoryAssistantHistory.AddAsync(entry, cancellationToken); await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task CompleteAsync(Guid historyId, string schemaVersion, string? resultJson, Guid? orientationPlanId, CancellationToken cancellationToken)
    {
        var entry = await db.RepositoryAssistantHistory.SingleOrDefaultAsync(x => x.Id == historyId, cancellationToken)
            ?? throw new NotFoundException("Assistant history entry was not found.");
        if (!RepositoryAssistantHistoryPolicy.ResultFits(resultJson, options.Value.MaximumResultBytes))
        {
            entry.FinishIncomplete(RepositoryAssistantHistoryStatus.Failed, timeProvider.GetUtcNow()); await db.SaveChangesAsync(cancellationToken); return;
        }
        var organizationUsage = await GetOrganizationUsageAsync(entry.OrganizationId, cancellationToken);
        if (!RepositoryAssistantHistoryPolicy.OrganizationHasRoom(organizationUsage, resultJson, options.Value.MaximumOrganizationStoredCharacters))
        {
            entry.FinishIncomplete(RepositoryAssistantHistoryStatus.Failed, timeProvider.GetUtcNow()); await db.SaveChangesAsync(cancellationToken); return;
        }
        entry.Complete(schemaVersion, resultJson, orientationPlanId, timeProvider.GetUtcNow()); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FinishIncompleteAsync(Guid historyId, RepositoryAssistantHistoryStatus status, CancellationToken cancellationToken)
    {
        var entry = await db.RepositoryAssistantHistory.SingleOrDefaultAsync(x => x.Id == historyId, cancellationToken);
        if (entry is null) return;
        entry.FinishIncomplete(status, timeProvider.GetUtcNow()); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RepositoryAssistantHistoryPage> ListAsync(Guid organizationId, Guid repositoryId, Guid userId,
        string? latestCommitSha, int page, int pageSize, CancellationToken cancellationToken)
    {
        await PruneExpiredAsync(cancellationToken);
        var query = db.RepositoryAssistantHistory.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId && x.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(x => x.IsStarred).ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new RepositoryAssistantHistoryPage(entries.Select(x => RepositoryAssistantHistoryMapping.ToSummary(x, latestCommitSha)).ToArray(), page, pageSize, total);
    }

    public Task<RepositoryAssistantHistory?> GetAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, CancellationToken cancellationToken) =>
        db.RepositoryAssistantHistory.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId && x.UserId == userId && x.Id == historyId, cancellationToken);

    public async Task SetStarredAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, bool isStarred, CancellationToken cancellationToken)
    {
        var entry = await GetTrackedAsync(organizationId, repositoryId, userId, historyId, cancellationToken);
        entry.SetStarred(isStarred, timeProvider.GetUtcNow()); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, string title, CancellationToken cancellationToken)
    {
        var entry = await GetTrackedAsync(organizationId, repositoryId, userId, historyId, cancellationToken);
        entry.Rename(title, timeProvider.GetUtcNow()); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, CancellationToken cancellationToken)
    {
        var entry = await GetTrackedAsync(organizationId, repositoryId, userId, historyId, cancellationToken);
        db.RepositoryAssistantHistory.Remove(entry); await db.SaveChangesAsync(cancellationToken);
    }

    public Task ClearAsync(Guid organizationId, Guid repositoryId, Guid userId, CancellationToken cancellationToken) =>
        db.RepositoryAssistantHistory.Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId && x.UserId == userId).ExecuteDeleteAsync(cancellationToken);

    private async Task<RepositoryAssistantHistory> GetTrackedAsync(Guid organizationId, Guid repositoryId, Guid userId, Guid historyId, CancellationToken cancellationToken) =>
        await db.RepositoryAssistantHistory.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId && x.UserId == userId && x.Id == historyId, cancellationToken)
        ?? throw new NotFoundException("Assistant history entry was not found.");

    private Task PruneExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = RepositoryAssistantHistoryPolicy.RetentionCutoff(timeProvider.GetUtcNow(), options.Value.RetentionDays);
        return db.RepositoryAssistantHistory.Where(x => x.CreatedAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<long> GetOrganizationUsageAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        long usage = 0;
        await foreach (var result in db.RepositoryAssistantHistory.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.ResultJson != null)
            .Select(x => x.ResultJson!)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            usage += result.Length;
            if (usage > options.Value.MaximumOrganizationStoredCharacters) break;
        }
        return usage;
    }

    private async Task MakeRoomAsync(Guid organizationId, Guid repositoryId, Guid userId, CancellationToken cancellationToken)
    {
        var excess = await db.RepositoryAssistantHistory.CountAsync(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId && x.UserId == userId, cancellationToken)
            - options.Value.MaximumEntriesPerUserRepository + 1;
        if (excess <= 0) return;
        var ids = await db.RepositoryAssistantHistory.Where(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId && x.UserId == userId)
            .OrderBy(x => x.IsStarred).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Select(x => x.Id).Take(excess).ToArrayAsync(cancellationToken);
        await db.RepositoryAssistantHistory.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}
