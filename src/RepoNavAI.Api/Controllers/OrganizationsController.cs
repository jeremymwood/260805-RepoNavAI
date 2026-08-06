using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;

namespace RepoNavAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<OrganizationSummary>> List(CancellationToken cancellationToken) => sender.Send(new ListOrganizationsQuery(), cancellationToken);

    [HttpGet("{organizationId:guid}")]
    public Task<OrganizationDetails> Get(Guid organizationId, CancellationToken cancellationToken) => sender.Send(new GetOrganizationQuery(organizationId), cancellationToken);

    [HttpPost]
    [ProducesResponseType<OrganizationSummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrganizationSummary>> Create(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var organization = await sender.Send(new CreateOrganizationCommand(request.Name), cancellationToken);
        return CreatedAtAction(nameof(Get), new { organizationId = organization.Id }, organization);
    }

    [HttpPatch("{organizationId:guid}")]
    public async Task<IActionResult> Rename(Guid organizationId, RenameOrganizationRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new RenameOrganizationCommand(organizationId, request.Name), cancellationToken);
        return NoContent();
    }

    [HttpPost("{organizationId:guid}/invitations")]
    [ProducesResponseType<InvitationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InvitationResult>> Invite(Guid organizationId, InviteMemberRequest request, CancellationToken cancellationToken)
    {
        var invitation = await sender.Send(new InviteOrganizationMemberCommand(organizationId, request.Email, request.Role), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, invitation);
    }

    [HttpPost("invitations/{token}/accept")]
    public Task<OrganizationSummary> AcceptInvitation(string token, CancellationToken cancellationToken) => sender.Send(new AcceptOrganizationInvitationCommand(token), cancellationToken);

    [HttpPatch("{organizationId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> ChangeRole(Guid organizationId, Guid userId, ChangeMemberRoleRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ChangeOrganizationMemberRoleCommand(organizationId, userId, request.Role), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{organizationId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveOrganizationMemberCommand(organizationId, userId), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateOrganizationRequest(string Name);
public sealed record RenameOrganizationRequest(string Name);
public sealed record InviteMemberRequest(string Email, OrganizationRole Role);
public sealed record ChangeMemberRoleRequest(OrganizationRole Role);
