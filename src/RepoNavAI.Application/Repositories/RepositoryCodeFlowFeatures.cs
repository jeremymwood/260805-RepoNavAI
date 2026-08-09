using FluentValidation;
using MediatR;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Application.Repositories;

public sealed record GenerateCodeFlowTraceCommand(Guid OrganizationId, Guid RepositoryId, string Question) : IRequest<CodeFlowTraceDto>;

public sealed class GenerateCodeFlowTraceValidator : AbstractValidator<GenerateCodeFlowTraceCommand>
{
    public GenerateCodeFlowTraceValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty(); RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
    }
}

public sealed class GenerateCodeFlowTraceHandler(IOrganizationAccess access, IRepositoryQueries repositories,
    IRepositoryOrientationStore snapshots, IEmbeddingGenerator embeddings, IVectorStore vectors,
    IRepositoryCodeFlowGenerator generator, IRepositoryChatSessionStore sessions, ICurrentUser currentUser)
    : IRequestHandler<GenerateCodeFlowTraceCommand, CodeFlowTraceDto>
{
    public async Task<CodeFlowTraceDto> Handle(GenerateCodeFlowTraceCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!await repositories.ExistsAsync(request.OrganizationId, request.RepositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
        if (!generator.IsConfigured) throw new ExternalServiceException("Code-flow explanation is not configured.");
        var snapshot = await snapshots.GetLatestSnapshotAsync(request.OrganizationId, request.RepositoryId, cancellationToken)
            ?? throw new ConflictException("Complete repository indexing before explaining a code flow.");
        var question = request.Question.Trim();
        var sessionId = await sessions.StartAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, generator.Model, cancellationToken);
        var completed = false;
        try
        {
            var retrievalQueries = new[]
            {
                question,
                $"entry point trigger controller handler method calls for: {question}",
                $"data state persistence async background retry cancellation error path for: {question}"
            };
            var queryEmbeddings = await embeddings.GenerateAsync(retrievalQueries, cancellationToken);
            var searches = new List<SemanticSearchResult>();
            foreach (var embedding in queryEmbeddings)
                searches.AddRange(await vectors.SearchAsync(request.OrganizationId, request.RepositoryId, embedding, 10, cancellationToken));
            var sources = searches.Where(x => string.Equals(x.CommitSha, snapshot.CommitSha, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.ChunkId).Select(group => group.OrderByDescending(x => x.Score).First())
                .OrderByDescending(x => x.Score).Take(20).ToArray();
            if (sources.Length == 0) throw new ConflictException("The indexed repository does not contain enough evidence to explain that flow.");
            var draft = await generator.GenerateAsync(question, sources, cancellationToken);
            var trace = CodeFlowMapping.Map(request.RepositoryId, snapshot.CommitSha, draft, sources);
            completed = true;
            return trace;
        }
        finally
        {
            var status = completed ? RepositoryChatStatus.Completed : cancellationToken.IsCancellationRequested ? RepositoryChatStatus.Cancelled : RepositoryChatStatus.Failed;
            await sessions.FinishAsync(sessionId, status, CancellationToken.None);
        }
    }
}

internal static class CodeFlowMapping
{
    public static CodeFlowTraceDto Map(Guid repositoryId, string commitSha, CodeFlowDraft draft, IReadOnlyList<SemanticSearchResult> sources)
    {
        if (draft.Steps is null || string.IsNullOrWhiteSpace(draft.Summary)) throw new ExternalServiceException("The code-flow provider returned an invalid trace.");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var steps = draft.Steps.Take(15).Select((step, index) =>
        {
            if (string.IsNullOrWhiteSpace(step.Key) || !keys.Add(step.Key) || string.IsNullOrWhiteSpace(step.Title) ||
                string.IsNullOrWhiteSpace(step.Component) || string.IsNullOrWhiteSpace(step.Symbol) || !Enum.IsDefined(step.Boundary) || !Enum.IsDefined(step.EvidenceLevel))
                throw new ExternalServiceException("The code-flow provider returned an invalid step.");
            var citations = (step.CitationNumbers ?? []).Distinct().Select(number => number >= 1 && number <= sources.Count
                ? sources[number - 1] : throw new ExternalServiceException("The code-flow provider returned an invalid citation."))
                .Select(source => new OrientationCitation(source.Path, source.StartLine, source.EndLine, source.CommitSha, source.SourceUrl)).ToArray();
            if (step.EvidenceLevel == OrientationEvidenceLevel.Confirmed && citations.Length == 0)
                throw new ExternalServiceException("The code-flow provider returned an unsupported confirmed step.");
            return new CodeFlowStep(step.Key, index + 1, step.Title, step.Component, step.Symbol, step.Responsibility,
                step.Handoff, step.Boundary, step.EvidenceLevel, citations);
        }).ToArray();
        if (steps.Length == 0) throw new ExternalServiceException("The code-flow provider returned an empty trace.");
        return new CodeFlowTraceDto("1.0", repositoryId, commitSha, draft.Summary, steps, draft.MissingEvidence?.Take(10).ToArray() ?? []);
    }
}
