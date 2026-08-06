using RepoNavAI.Domain.Organizations;

namespace RepoNavAI.Application.Organizations;

public sealed record OrganizationSummary(Guid Id, string Name, string Slug, OrganizationRole Role);
public sealed record OrganizationMemberDto(Guid UserId, string Email, string DisplayName, OrganizationRole Role);
public sealed record OrganizationDetails(Guid Id, string Name, string Slug, OrganizationRole CurrentUserRole, IReadOnlyCollection<OrganizationMemberDto> Members);
public sealed record InvitationResult(Guid InvitationId, string Token, DateTimeOffset ExpiresAtUtc);
public sealed record PendingInvitationDto(Guid Id, string Email, OrganizationRole Role, string InvitedByDisplayName, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);

public interface IOrganizationRepository
{
    Task AddAsync(Organization organization, CancellationToken cancellationToken);
    Task<Organization?> GetWithMembersAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);
    Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken);
    Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<OrganizationInvitation?> GetInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken);
    Task<bool> HasPendingInvitationAsync(Guid organizationId, string email, CancellationToken cancellationToken);
    Task<bool> IsEmailMemberAsync(Guid organizationId, string email, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IOrganizationQueries
{
    Task<IReadOnlyCollection<OrganizationSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<OrganizationDetails?> GetForUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PendingInvitationDto>> ListPendingInvitationsAsync(Guid organizationId, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IInvitationTokenService
{
    string CreateToken();
    string HashToken(string token);
}
