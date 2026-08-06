using Microsoft.EntityFrameworkCore;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Infrastructure.Identity;
using RepoNavAI.Infrastructure.Persistence;

namespace RepoNavAI.Infrastructure.Organizations;

public sealed class OrganizationRepository(AppDbContext dbContext) : IOrganizationRepository
{
    public async Task AddAsync(Organization organization, CancellationToken cancellationToken) => await dbContext.Organizations.AddAsync(organization, cancellationToken);

    public Task<Organization?> GetWithMembersAsync(Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.Organizations.Include(x => x.Members).SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) => dbContext.Organizations.AnyAsync(x => x.Slug == slug, cancellationToken);

    public async Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken) => await dbContext.OrganizationInvitations.AddAsync(invitation, cancellationToken);

    public Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.OrganizationInvitations.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task<bool> HasPendingInvitationAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        dbContext.OrganizationInvitations.AnyAsync(x => x.OrganizationId == organizationId && x.Email == email && x.AcceptedAtUtc == null && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTimeOffset.UtcNow, cancellationToken);

    public Task<bool> IsEmailMemberAsync(Guid organizationId, string email, CancellationToken cancellationToken) =>
        (from member in dbContext.OrganizationMembers
         join user in dbContext.Set<ApplicationUser>() on member.UserId equals user.Id
         where member.OrganizationId == organizationId && user.NormalizedEmail == email.ToUpperInvariant()
         select member).AnyAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class OrganizationQueries(AppDbContext dbContext) : IOrganizationQueries
{
    public async Task<IReadOnlyCollection<OrganizationSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await (from member in dbContext.OrganizationMembers.AsNoTracking()
               join organization in dbContext.Organizations.AsNoTracking() on member.OrganizationId equals organization.Id
               where member.UserId == userId
               orderby organization.Name
               select new OrganizationSummary(organization.Id, organization.Name, organization.Slug, member.Role))
            .ToArrayAsync(cancellationToken);

    public async Task<OrganizationDetails?> GetForUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var organization = await (from member in dbContext.OrganizationMembers.AsNoTracking()
                                  join candidate in dbContext.Organizations.AsNoTracking() on member.OrganizationId equals candidate.Id
                                  where member.UserId == userId && candidate.Id == organizationId
                                  select new { candidate.Id, candidate.Name, candidate.Slug, member.Role }).SingleOrDefaultAsync(cancellationToken);
        if (organization is null) return null;
        var members = await (from member in dbContext.OrganizationMembers.AsNoTracking()
                             join user in dbContext.Set<ApplicationUser>().AsNoTracking() on member.UserId equals user.Id
                             where member.OrganizationId == organizationId
                             orderby user.DisplayName
                             select new OrganizationMemberDto(user.Id, user.Email!, user.DisplayName, member.Role)).ToArrayAsync(cancellationToken);
        return new OrganizationDetails(organization.Id, organization.Name, organization.Slug, organization.Role, members);
    }
}
