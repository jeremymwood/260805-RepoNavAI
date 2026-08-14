using System.Text.Json;
using FluentValidation;
using MediatR;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Application.Repositories;

public sealed record CreateOrientationPlanCommand(Guid OrganizationId, Guid RepositoryId, OrientationRole Role,
    OrientationExperience Experience, OrientationFocus Focus, int TimeBudgetMinutes, string? Objective) : IRequest<OrientationPlanDto>;
public sealed record GetOrientationPlanQuery(Guid OrganizationId, Guid RepositoryId) : IRequest<OrientationPlanDto?>;
public sealed record UpdateOrientationProgressCommand(Guid OrganizationId, Guid RepositoryId, Guid PlanId, IReadOnlyCollection<string> CompletedStepKeys) : IRequest<OrientationPlanDto>;

public sealed class CreateOrientationPlanValidator : AbstractValidator<CreateOrientationPlanCommand>
{
    public CreateOrientationPlanValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty(); RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum(); RuleFor(x => x.Experience).IsInEnum(); RuleFor(x => x.Focus).IsInEnum();
        RuleFor(x => x.TimeBudgetMinutes).InclusiveBetween(15, 480);
        RuleFor(x => x.Objective).MaximumLength(500);
    }
}

public sealed class CreateOrientationPlanHandler(IOrganizationAccess access, IRepositoryQueries repositories,
    IRepositoryOrientationStore store, IEmbeddingGenerator embeddings, IVectorStore vectors,
    IRepositoryOrientationGenerator generator, IRepositoryAssistantHistoryStore history, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<CreateOrientationPlanCommand, OrientationPlanDto>
{
    private const string RetrievalPrompt = "application purpose architecture solution structure request entry points data flow domain concepts dependencies configuration operations deployment tests safe first changes";

    public async Task<OrientationPlanDto> Handle(CreateOrientationPlanCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!await repositories.ExistsAsync(request.OrganizationId, request.RepositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
        if (!generator.IsConfigured) throw new ExternalServiceException("Repository orientation is not configured.");
        var snapshot = await store.GetLatestSnapshotAsync(request.OrganizationId, request.RepositoryId, cancellationToken)
            ?? throw new ConflictException("Complete repository indexing before creating an orientation plan.");
        var profile = new OrientationProfile(request.Role, request.Experience, request.Focus, request.TimeBudgetMinutes, request.Objective?.Trim());
        var prompt = string.IsNullOrWhiteSpace(request.Objective) ? $"Create a {request.Focus} orientation plan." : request.Objective.Trim();
        var historyEntry = await history.StartAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, RepositoryAssistantHistoryMode.Orientation, prompt, snapshot.CommitSha, cancellationToken);
        try
        {
        var embedding = (await embeddings.GenerateAsync([RetrievalPrompt + " " + request.Focus + " " + request.Objective], cancellationToken))[0];
        var sources = await vectors.SearchAsync(request.OrganizationId, request.RepositoryId, embedding, 20, cancellationToken);
        if (sources.Count == 0) throw new ConflictException("The indexed repository does not contain enough evidence for an orientation plan.");
        var draft = await generator.GenerateAsync(profile, sources, cancellationToken);
        var stepKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var steps = draft.Steps.Take(12).Select(step =>
        {
            if (string.IsNullOrWhiteSpace(step.Key) || !stepKeys.Add(step.Key) || string.IsNullOrWhiteSpace(step.Title)) throw new ExternalServiceException("The orientation provider returned an invalid plan.");
            if (!Enum.IsDefined(step.EvidenceLevel)) throw new ExternalServiceException("The orientation provider returned an invalid evidence level.");
            var citations = step.CitationNumbers.Distinct().Select(number => number >= 1 && number <= sources.Count
                ? sources.ElementAt(number - 1) : throw new ExternalServiceException("The orientation provider returned an invalid citation."))
                .Select(source => new OrientationCitation(source.Path, source.StartLine, source.EndLine, source.CommitSha, source.SourceUrl)).ToArray();
            if (step.EvidenceLevel == OrientationEvidenceLevel.Confirmed && citations.Length == 0) throw new ExternalServiceException("The orientation provider returned an unsupported factual step.");
            return new OrientationStep(step.Key, step.Title, step.Objective, step.Evidence, step.EvidenceLevel, citations, false);
        }).ToArray();
        if (steps.Length == 0) throw new ExternalServiceException("The orientation provider returned an empty plan.");
        var content = new StoredOrientation(draft.Summary, steps, draft.MissingEvidence.Take(10).ToArray());
        var entity = new RepositoryOrientationPlan(request.OrganizationId, request.RepositoryId, currentUser.UserId, snapshot.Id,
            snapshot.CommitSha, request.Role, request.Experience, request.Focus, request.TimeBudgetMinutes, generator.Model,
            JsonSerializer.Serialize(content), timeProvider.GetUtcNow());
        await store.AddAsync(entity, cancellationToken); await store.SaveChangesAsync(cancellationToken);
        await history.CompleteAsync(historyEntry.Id, RepositoryAssistantHistorySchemas.OrientationV1, null, entity.Id, cancellationToken);
        return OrientationPlanMapping.ToDto(entity, content, snapshot.CommitSha);
        }
        catch
        {
            await history.FinishIncompleteAsync(historyEntry.Id, cancellationToken.IsCancellationRequested ? RepositoryAssistantHistoryStatus.Cancelled : RepositoryAssistantHistoryStatus.Failed, CancellationToken.None); throw;
        }
    }
}

public sealed class GetOrientationPlanHandler(IOrganizationAccess access, IRepositoryOrientationStore store, ICurrentUser currentUser)
    : IRequestHandler<GetOrientationPlanQuery, OrientationPlanDto?>
{
    public async Task<OrientationPlanDto?> Handle(GetOrientationPlanQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        var plan = await store.GetLatestAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, cancellationToken);
        if (plan is null) return null;
        var latest = await store.GetLatestSnapshotAsync(request.OrganizationId, request.RepositoryId, cancellationToken);
        return OrientationPlanMapping.ToDto(plan, JsonSerializer.Deserialize<StoredOrientation>(plan.PlanJson)!, latest?.CommitSha);
    }
}

public sealed class UpdateOrientationProgressHandler(IOrganizationAccess access, IRepositoryOrientationStore store,
    ICurrentUser currentUser, TimeProvider timeProvider) : IRequestHandler<UpdateOrientationProgressCommand, OrientationPlanDto>
{
    public async Task<OrientationPlanDto> Handle(UpdateOrientationProgressCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        var plan = await store.GetAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, request.PlanId, cancellationToken) ?? throw new NotFoundException("Orientation plan was not found.");
        var content = JsonSerializer.Deserialize<StoredOrientation>(plan.PlanJson)!;
        var valid = content.Steps.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var completed = request.CompletedStepKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (completed.Any(key => !valid.Contains(key))) throw new ValidationException("One or more orientation steps are invalid.");
        plan.SetProgress(JsonSerializer.Serialize(completed), timeProvider.GetUtcNow()); await store.SaveChangesAsync(cancellationToken);
        var latest = await store.GetLatestSnapshotAsync(request.OrganizationId, request.RepositoryId, cancellationToken);
        return OrientationPlanMapping.ToDto(plan, content, latest?.CommitSha);
    }
}

public sealed record StoredOrientation(string Summary, IReadOnlyCollection<OrientationStep> Steps, IReadOnlyCollection<string> MissingEvidence);
internal static class OrientationPlanMapping
{
    public static OrientationPlanDto ToDto(RepositoryOrientationPlan plan, StoredOrientation content, string? latestCommit)
    {
        var completed = JsonSerializer.Deserialize<string[]>(plan.CompletedStepKeysJson)?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        return new(plan.Id, plan.RepositoryId, plan.CommitSha, plan.Role, plan.Experience, plan.Focus, plan.TimeBudgetMinutes,
            content.Summary, content.Steps.Select(x => x with { Completed = completed.Contains(x.Key) }).ToArray(), content.MissingEvidence,
            latestCommit is not null && !string.Equals(latestCommit, plan.CommitSha, StringComparison.OrdinalIgnoreCase), plan.CreatedAtUtc);
    }
}
