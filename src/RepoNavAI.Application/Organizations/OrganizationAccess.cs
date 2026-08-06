using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Domain.Organizations;

namespace RepoNavAI.Application.Organizations;

public interface IOrganizationAccess
{
    Task<Organization> RequireAsync(Guid organizationId, Guid userId, OrganizationRole minimumRole, CancellationToken cancellationToken);
}

public sealed class OrganizationAccess(IOrganizationRepository repository) : IOrganizationAccess
{
    public async Task<Organization> RequireAsync(Guid organizationId, Guid userId, OrganizationRole minimumRole, CancellationToken cancellationToken)
    {
        var organization = await repository.GetWithMembersAsync(organizationId, cancellationToken);
        if (organization is null) throw new NotFoundException("Organization was not found.");
        var member = organization.Members.SingleOrDefault(x => x.UserId == userId);
        if (member is null) throw new NotFoundException("Organization was not found.");
        if (!HasAccess(member.Role, minimumRole)) throw new ForbiddenException("You do not have permission to perform this action.");
        return organization;
    }

    private static bool HasAccess(OrganizationRole actual, OrganizationRole required) => actual switch
    {
        OrganizationRole.Owner => true,
        OrganizationRole.Administrator => required is OrganizationRole.Administrator or OrganizationRole.Member,
        OrganizationRole.Member => required is OrganizationRole.Member,
        _ => false
    };
}
