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

    [HttpGet("{repositoryId:guid}/indexing")]
    public Task<IndexingRequestDto> GetIndexingStatus(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) => sender.Send(new GetIndexingStatusQuery(organizationId, repositoryId), cancellationToken);

    [HttpPost("{repositoryId:guid}/indexing/cancel")]
    public async Task<IActionResult> Cancel(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) { await sender.Send(new CancelIndexingCommand(organizationId, repositoryId), cancellationToken); return NoContent(); }

    [HttpPost("{repositoryId:guid}/indexing/retry")]
    public async Task<IActionResult> Retry(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) { await sender.Send(new RetryIndexingCommand(organizationId, repositoryId), cancellationToken); return NoContent(); }
}

public sealed record RegisterRepositoryRequest(string Url);
