using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepoNavAI.Application.Repositories;

namespace RepoNavAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:guid}/repositories")]
public sealed class RepositoriesController(ISender sender, ILogger<RepositoriesController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
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

    [HttpPost("{repositoryId:guid}/indexing/reindex")]
    public async Task<IActionResult> Reindex(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) { await sender.Send(new ReindexRepositoryCommand(organizationId, repositoryId), cancellationToken); return Accepted(); }

    [HttpGet("{repositoryId:guid}/endpoints")]
    public Task<IReadOnlyCollection<RepositoryEndpointDto>> ListEndpoints(Guid organizationId, Guid repositoryId, [FromQuery] string? method, [FromQuery] string? search, [FromQuery] bool? requiresAuthorization, CancellationToken cancellationToken) =>
        sender.Send(new ListRepositoryEndpointsQuery(organizationId, repositoryId, method, search, requiresAuthorization), cancellationToken);

    [HttpGet("{repositoryId:guid}/semantic-search")]
    public Task<IReadOnlyCollection<SemanticSearchResult>> SemanticSearch(Guid organizationId, Guid repositoryId, [FromQuery] string query, [FromQuery] int limit = 10, CancellationToken cancellationToken = default) =>
        sender.Send(new SemanticSearchQuery(organizationId, repositoryId, query, limit), cancellationToken);

    [HttpPost("{repositoryId:guid}/chat")]
    [Produces("text/event-stream")]
    public async Task Chat(Guid organizationId, Guid repositoryId, RepositoryChatRequest request, CancellationToken cancellationToken)
    {
        await using var stream = sender.CreateStream(new StreamRepositoryChatQuery(organizationId, repositoryId, request.Question), cancellationToken).GetAsyncEnumerator(cancellationToken);
        if (!await stream.MoveNextAsync()) return;

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Append("X-Accel-Buffering", "no");
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await WriteEventAsync(stream.Current, cancellationToken);

        try
        {
            while (await stream.MoveNextAsync()) await WriteEventAsync(stream.Current, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Repository chat stream failed for organization {OrganizationId} and repository {RepositoryId}", organizationId, repositoryId);
            if (!cancellationToken.IsCancellationRequested)
                await WriteEventAsync(new RepositoryChatEvent(RepositoryChatEventType.Error, "The answer provider could not complete this response. Please retry."), cancellationToken);
        }
    }

    [HttpGet("{repositoryId:guid}/orientation-plan")]
    public Task<OrientationPlanDto?> GetOrientationPlan(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) =>
        sender.Send(new GetOrientationPlanQuery(organizationId, repositoryId), cancellationToken);

    [HttpPost("{repositoryId:guid}/orientation-plan")]
    [ProducesResponseType<OrientationPlanDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrientationPlanDto>> CreateOrientationPlan(Guid organizationId, Guid repositoryId, CreateOrientationPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await sender.Send(new CreateOrientationPlanCommand(organizationId, repositoryId, request.Role, request.Experience, request.Focus, request.TimeBudgetMinutes, request.Objective), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, plan);
    }

    [HttpPut("{repositoryId:guid}/orientation-plan/{planId:guid}/progress")]
    public Task<OrientationPlanDto> UpdateOrientationProgress(Guid organizationId, Guid repositoryId, Guid planId, UpdateOrientationProgressRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateOrientationProgressCommand(organizationId, repositoryId, planId, request.CompletedStepKeys), cancellationToken);

    private async Task WriteEventAsync(RepositoryChatEvent chatEvent, CancellationToken cancellationToken)
    {
        var eventName = chatEvent.Type.ToString().ToLowerInvariant();
        var payload = JsonSerializer.Serialize(chatEvent, StreamJsonOptions);
        await Response.WriteAsync($"event: {eventName}\ndata: {payload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

public sealed record RegisterRepositoryRequest(string Url);
public sealed record RepositoryChatRequest(string Question);
public sealed record CreateOrientationPlanRequest(RepoNavAI.Domain.Repositories.OrientationRole Role, RepoNavAI.Domain.Repositories.OrientationExperience Experience, RepoNavAI.Domain.Repositories.OrientationFocus Focus, int TimeBudgetMinutes, string? Objective);
public sealed record UpdateOrientationProgressRequest(IReadOnlyCollection<string> CompletedStepKeys);
