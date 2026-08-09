using FluentValidation;
using MediatR;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;

namespace RepoNavAI.Application.Repositories;

public enum RepositoryAssistantIntent { Search, Answer, Orientation, CodeFlow }
public sealed record ResolveRepositoryAssistantIntentQuery(Guid OrganizationId, Guid RepositoryId, string Prompt) : IRequest<RepositoryAssistantIntentDto>;
public sealed record RepositoryAssistantIntentDto(string SchemaVersion, RepositoryAssistantIntent Intent, string Reason);

public sealed class ResolveRepositoryAssistantIntentValidator : AbstractValidator<ResolveRepositoryAssistantIntentQuery>
{
    public ResolveRepositoryAssistantIntentValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty(); RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ResolveRepositoryAssistantIntentHandler(IOrganizationAccess access, IRepositoryQueries repositories, ICurrentUser currentUser)
    : IRequestHandler<ResolveRepositoryAssistantIntentQuery, RepositoryAssistantIntentDto>
{
    public async Task<RepositoryAssistantIntentDto> Handle(ResolveRepositoryAssistantIntentQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!await repositories.ExistsAsync(request.OrganizationId, request.RepositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
        return RepositoryAssistantIntentResolver.Resolve(request.Prompt);
    }
}

internal static class RepositoryAssistantIntentResolver
{
    private static readonly string[] OrientationTerms = ["onboard", "onboarding", "orientation", "learning plan", "study plan", "new to", "get up to speed", "where should i start"];
    private static readonly string[] CodeFlowTerms = ["trace ", "code flow", "execution flow", "request flow", "function-to-function", "through the code", "sequence of calls", "call path"];
    private static readonly string[] SearchTerms = ["find ", "locate ", "where is", "which file", "which class", "search for", "show me files", "references to"];

    public static RepositoryAssistantIntentDto Resolve(string prompt)
    {
        var normalized = prompt.Trim().ToLowerInvariant();
        if (OrientationTerms.Any(normalized.Contains)) return new("1.0", RepositoryAssistantIntent.Orientation, "The prompt asks for an onboarding or learning plan.");
        if (CodeFlowTerms.Any(normalized.Contains) || (normalized.StartsWith("how does ") && normalized.Contains(" work")))
            return new("1.0", RepositoryAssistantIntent.CodeFlow, "The prompt asks for an ordered execution or call flow.");
        if (SearchTerms.Any(normalized.Contains)) return new("1.0", RepositoryAssistantIntent.Search, "The prompt asks to locate repository source.");
        return new("1.0", RepositoryAssistantIntent.Answer, "The prompt is best handled as a cited repository question.");
    }
}
