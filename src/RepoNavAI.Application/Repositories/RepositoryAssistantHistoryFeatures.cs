using System.Text.Json;
using FluentValidation;
using MediatR;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Application.Repositories;

public static class RepositoryAssistantHistorySchemas
{
    public const string SearchV1 = "search/1";
    public const string AnswerV1 = "answer/1";
    public const string OrientationV1 = "orientation/1";
    public const string CodeFlowV1 = "code-flow/1";

    public static bool IsSupported(RepositoryAssistantHistoryMode mode, string? version) => (mode, version) switch
    {
        (RepositoryAssistantHistoryMode.Search, SearchV1) => true,
        (RepositoryAssistantHistoryMode.Answer, AnswerV1) => true,
        (RepositoryAssistantHistoryMode.Orientation, OrientationV1) => true,
        (RepositoryAssistantHistoryMode.CodeFlow, CodeFlowV1) => true,
        _ => false
    };
}

public sealed record ListRepositoryAssistantHistoryQuery(Guid OrganizationId, Guid RepositoryId, int Page = 1, int PageSize = 10) : IRequest<RepositoryAssistantHistoryPage>;
public sealed record GetRepositoryAssistantHistoryQuery(Guid OrganizationId, Guid RepositoryId, Guid HistoryId) : IRequest<RepositoryAssistantHistoryDetailDto>;
public sealed record SetRepositoryAssistantHistoryStarCommand(Guid OrganizationId, Guid RepositoryId, Guid HistoryId, bool IsStarred) : IRequest;
public sealed record RenameRepositoryAssistantHistoryCommand(Guid OrganizationId, Guid RepositoryId, Guid HistoryId, string Title) : IRequest;
public sealed record DeleteRepositoryAssistantHistoryCommand(Guid OrganizationId, Guid RepositoryId, Guid HistoryId) : IRequest;
public sealed record ClearRepositoryAssistantHistoryCommand(Guid OrganizationId, Guid RepositoryId, string Confirmation) : IRequest;

public sealed class RenameRepositoryAssistantHistoryValidator : AbstractValidator<RenameRepositoryAssistantHistoryCommand>
{
    public RenameRepositoryAssistantHistoryValidator() => RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
}

public sealed class ListRepositoryAssistantHistoryHandler(IOrganizationAccess access, IRepositoryQueries repositories,
    IRepositoryOrientationStore snapshots, IRepositoryAssistantHistoryStore history, ICurrentUser currentUser)
    : IRequestHandler<ListRepositoryAssistantHistoryQuery, RepositoryAssistantHistoryPage>
{
    public async Task<RepositoryAssistantHistoryPage> Handle(ListRepositoryAssistantHistoryQuery request, CancellationToken cancellationToken)
    {
        await RepositoryAssistantHistoryAuthorization.RequireRepositoryAsync(access, repositories, currentUser, request.OrganizationId, request.RepositoryId, cancellationToken);
        if (request.Page < 1 || request.PageSize is < 1 or > 50) throw new ValidationException("Page must be positive and page size must be between 1 and 50.");
        var latest = await snapshots.GetLatestSnapshotAsync(request.OrganizationId, request.RepositoryId, cancellationToken);
        return await history.ListAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, latest?.CommitSha, request.Page, request.PageSize, cancellationToken);
    }
}

public sealed class GetRepositoryAssistantHistoryHandler(IOrganizationAccess access, IRepositoryQueries repositories,
    IRepositoryOrientationStore orientations, IRepositoryAssistantHistoryStore history, ICurrentUser currentUser)
    : IRequestHandler<GetRepositoryAssistantHistoryQuery, RepositoryAssistantHistoryDetailDto>
{
    public async Task<RepositoryAssistantHistoryDetailDto> Handle(GetRepositoryAssistantHistoryQuery request, CancellationToken cancellationToken)
    {
        await RepositoryAssistantHistoryAuthorization.RequireRepositoryAsync(access, repositories, currentUser, request.OrganizationId, request.RepositoryId, cancellationToken);
        var entry = await history.GetAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, request.HistoryId, cancellationToken)
            ?? throw new NotFoundException("Assistant history entry was not found.");
        var latest = await orientations.GetLatestSnapshotAsync(request.OrganizationId, request.RepositoryId, cancellationToken);
        var summary = RepositoryAssistantHistoryMapping.ToSummary(entry, latest?.CommitSha);
        if (!summary.IsSupported || entry.Status != RepositoryAssistantHistoryStatus.Completed) return new(summary, null);

        try
        {
            object? result = entry.Mode switch
            {
                RepositoryAssistantHistoryMode.Search => Deserialize<StoredSearchHistory>(entry.ResultJson),
                RepositoryAssistantHistoryMode.Answer => Deserialize<StoredAnswerHistory>(entry.ResultJson),
                RepositoryAssistantHistoryMode.CodeFlow => ToCodeFlow(Deserialize<StoredCodeFlowHistory>(entry.ResultJson)),
                RepositoryAssistantHistoryMode.Orientation => await GetOrientationAsync(entry, orientations, currentUser.UserId, latest?.CommitSha, cancellationToken),
                _ => null
            };
            return result is null ? new(summary with { IsSupported = false }, null) : new(summary, result);
        }
        catch (JsonException) { return new(summary with { IsSupported = false }, null); }
    }

    private static T? Deserialize<T>(string? json) => string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);
    private static CodeFlowTraceDto? ToCodeFlow(StoredCodeFlowHistory? stored) => stored is null ? null
        : new(stored.SchemaVersion, stored.RepositoryId, stored.CommitSha, stored.Summary, stored.Steps, stored.MissingEvidence, []);
    private static async Task<OrientationPlanDto?> GetOrientationAsync(RepositoryAssistantHistory entry, IRepositoryOrientationStore orientations,
        Guid userId, string? latestCommit, CancellationToken cancellationToken)
    {
        if (entry.OrientationPlanId is not { } planId) return null;
        var plan = await orientations.GetAsync(entry.OrganizationId, entry.RepositoryId, userId, planId, cancellationToken);
        return plan is null ? null : OrientationPlanMapping.ToDto(plan, JsonSerializer.Deserialize<StoredOrientation>(plan.PlanJson)!, latestCommit);
    }
}

public sealed class SetRepositoryAssistantHistoryStarHandler(IOrganizationAccess access, IRepositoryAssistantHistoryStore history, ICurrentUser currentUser)
    : IRequestHandler<SetRepositoryAssistantHistoryStarCommand>
{
    public async Task Handle(SetRepositoryAssistantHistoryStarCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        await history.SetStarredAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, request.HistoryId, request.IsStarred, cancellationToken);
    }
}

public sealed class RenameRepositoryAssistantHistoryHandler(IOrganizationAccess access, IRepositoryAssistantHistoryStore history, ICurrentUser currentUser)
    : IRequestHandler<RenameRepositoryAssistantHistoryCommand>
{
    public async Task Handle(RenameRepositoryAssistantHistoryCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        await history.RenameAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, request.HistoryId, request.Title.Trim(), cancellationToken);
    }
}

public sealed class DeleteRepositoryAssistantHistoryHandler(IOrganizationAccess access, IRepositoryAssistantHistoryStore history, ICurrentUser currentUser)
    : IRequestHandler<DeleteRepositoryAssistantHistoryCommand>
{
    public async Task Handle(DeleteRepositoryAssistantHistoryCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        await history.DeleteAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, request.HistoryId, cancellationToken);
    }
}

public sealed class ClearRepositoryAssistantHistoryHandler(IOrganizationAccess access, IRepositoryAssistantHistoryStore history, ICurrentUser currentUser)
    : IRequestHandler<ClearRepositoryAssistantHistoryCommand>
{
    public async Task Handle(ClearRepositoryAssistantHistoryCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!string.Equals(request.Confirmation.Trim(), "CLEAR", StringComparison.Ordinal)) throw new ValidationException("Enter CLEAR to permanently clear assistant history.");
        await history.ClearAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, cancellationToken);
    }
}

public static class RepositoryAssistantHistoryMapping
{
    public static RepositoryAssistantHistorySummaryDto ToSummary(RepositoryAssistantHistory entry, string? latestCommit) =>
        new(entry.Id, entry.Mode, entry.Status, entry.Prompt, entry.DisplayTitle, entry.CommitSha, entry.SchemaVersion,
            entry.IsStarred, latestCommit is not null && !string.Equals(latestCommit, entry.CommitSha, StringComparison.OrdinalIgnoreCase),
            RepositoryAssistantHistorySchemas.IsSupported(entry.Mode, entry.SchemaVersion), entry.CreatedAtUtc, entry.CompletedAtUtc);
}

internal static class RepositoryAssistantHistoryAuthorization
{
    public static async Task RequireRepositoryAsync(IOrganizationAccess access, IRepositoryQueries repositories, ICurrentUser currentUser,
        Guid organizationId, Guid repositoryId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!await repositories.ExistsAsync(organizationId, repositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
    }
}
