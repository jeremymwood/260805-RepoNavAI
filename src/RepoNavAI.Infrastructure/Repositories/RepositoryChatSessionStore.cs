using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Persistence;

namespace RepoNavAI.Infrastructure.Repositories;

public sealed class RepositoryChatSessionStore(AppDbContext db, TimeProvider timeProvider, IOptions<RepositoryChatOptions> options) : IRepositoryChatSessionStore
{
    public async Task<Guid> StartAsync(Guid organizationId, Guid repositoryId, Guid userId, string model, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow(); var since = now.AddHours(-24);
        var usage = await db.RepositoryChatSessions.CountAsync(x => x.OrganizationId == organizationId && x.CreatedAtUtc >= since, cancellationToken);
        if (usage >= options.Value.OrganizationDailyRequestLimit) throw new RateLimitException("This organization has reached its repository chat limit. Try again later.");
        var session = new RepositoryChatSession(organizationId, repositoryId, userId, model, now);
        db.RepositoryChatSessions.Add(session); await db.SaveChangesAsync(cancellationToken); return session.Id;
    }

    public async Task FinishAsync(Guid sessionId, RepositoryChatStatus status, CancellationToken cancellationToken)
    {
        var session = await db.RepositoryChatSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return;
        session.Finish(status, timeProvider.GetUtcNow()); await db.SaveChangesAsync(cancellationToken);
    }
}
