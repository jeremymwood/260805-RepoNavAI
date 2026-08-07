using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoNavAI.Application.Repositories;

namespace RepoNavAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:guid}/repositories")]
public sealed class RepositoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<RepositoryDto>> List(Guid organizationId, CancellationToken cancellationToken) =>
        sender.Send(new ListRepositoriesQuery(organizationId), cancellationToken);

    [HttpPost]
    [ProducesResponseType<RepositoryDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RepositoryDto>> Register(Guid organizationId, RegisterRepositoryRequest request, CancellationToken cancellationToken)
    {
        var repository = await sender.Send(new RegisterRepositoryCommand(organizationId, request.Url), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, repository);
    }
}

public sealed record RegisterRepositoryRequest(string Url);
