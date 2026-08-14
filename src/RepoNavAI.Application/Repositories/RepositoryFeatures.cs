using FluentValidation;
using MediatR;
using System.Runtime.CompilerServices;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Application.Repositories;

public sealed record RegisterRepositoryCommand(Guid OrganizationId, string Url) : IRequest<RepositoryDto>;
public sealed record ListRepositoriesQuery(Guid OrganizationId, int Page = 1, int PageSize = 10) : IRequest<RepositoryPage>;
public sealed record SetRepositoryFavoriteCommand(Guid OrganizationId, Guid RepositoryId, bool IsFavorite) : IRequest;
public sealed record GetIndexingStatusQuery(Guid OrganizationId, Guid RepositoryId) : IRequest<IndexingRequestDto>;
public sealed record CancelIndexingCommand(Guid OrganizationId, Guid RepositoryId) : IRequest;
public sealed record RetryIndexingCommand(Guid OrganizationId, Guid RepositoryId) : IRequest;
public sealed record ReindexRepositoryCommand(Guid OrganizationId, Guid RepositoryId) : IRequest;
public sealed record RemoveRepositoryCommand(Guid OrganizationId, Guid RepositoryId, string Confirmation) : IRequest;
public sealed record ListRepositoryEndpointsQuery(Guid OrganizationId, Guid RepositoryId, string? Method, string? Search, bool? RequiresAuthorization) : IRequest<IReadOnlyCollection<RepositoryEndpointDto>>;
public sealed record GetRepositoryCapabilitiesQuery(Guid OrganizationId, Guid RepositoryId) : IRequest<RepositoryCapabilitiesDto>;
public sealed record SemanticSearchQuery(Guid OrganizationId, Guid RepositoryId, string Query, int Limit = 10) : IRequest<IReadOnlyCollection<SemanticSearchResult>>;
public sealed record StreamRepositoryChatQuery(Guid OrganizationId, Guid RepositoryId, string Question) : IStreamRequest<RepositoryChatEvent>;

public sealed class RegisterRepositoryValidator : AbstractValidator<RegisterRepositoryCommand>
{
    public RegisterRepositoryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048).Must(value => GitHubRepositoryAddress.TryParse(value, out _))
            .WithMessage("Enter a valid HTTPS GitHub repository URL, for example https://github.com/owner/repository.");
    }
}

public sealed class SemanticSearchValidator : AbstractValidator<SemanticSearchQuery>
{
    public SemanticSearchValidator() { RuleFor(x => x.Query).NotEmpty().MaximumLength(2000); RuleFor(x => x.Limit).InclusiveBetween(1, 25); }
}

public sealed class StreamRepositoryChatValidator : AbstractValidator<StreamRepositoryChatQuery>
{
    public StreamRepositoryChatValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
    }
}

public sealed class RegisterRepositoryHandler(IOrganizationAccess access, IRepositoryProvider provider, IRepositoryRegistrationRepository repository, ICurrentUser currentUser)
    : IRequestHandler<RegisterRepositoryCommand, RepositoryDto>
{
    public async Task<RepositoryDto> Handle(RegisterRepositoryCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!GitHubRepositoryAddress.TryParse(request.Url, out var address) || address is null) throw new ValidationException("Repository URL is invalid.");
        if (await repository.ExistsAsync(request.OrganizationId, address.Owner, address.Name, cancellationToken)) throw new ConflictException("Repository is already registered in this organization.");
        var verified = await provider.GetAsync(address, cancellationToken) ?? throw new NotFoundException("Repository was not found or is not accessible to the configured GitHub integration.");
        if (await repository.ExistsAsync(request.OrganizationId, verified.Owner, verified.Name, cancellationToken)) throw new ConflictException("Repository is already registered in this organization.");
        var registered = new RegisteredRepository(request.OrganizationId, verified.ProviderRepositoryId, verified.Owner, verified.Name, verified.DefaultBranch, verified.Visibility, verified.WebUrl, currentUser.UserId);
        var indexingRequest = new RepositoryIndexingRequest(request.OrganizationId, registered.Id, currentUser.UserId);
        await repository.AddAsync(registered, indexingRequest, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new RepositoryDto(registered.Id, registered.OrganizationId, registered.Owner, registered.Name, registered.FullName, registered.DefaultBranch, registered.Visibility, registered.WebUrl, indexingRequest.Status, registered.CreatedAtUtc);
    }
}

public sealed class ListRepositoriesHandler(IOrganizationAccess access, IRepositoryQueries queries, ICurrentUser currentUser)
    : IRequestHandler<ListRepositoriesQuery, RepositoryPage>
{
    public async Task<RepositoryPage> Handle(ListRepositoriesQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (request.Page < 1 || request.PageSize is < 1 or > 50) throw new ValidationException("Page must be positive and page size must be between 1 and 50.");
        return await queries.ListAsync(request.OrganizationId, currentUser.UserId, request.Page, request.PageSize, cancellationToken);
    }
}

public sealed class SetRepositoryFavoriteHandler(IOrganizationAccess access, IRepositoryQueries queries, IRepositoryFavoriteStore favorites, ICurrentUser currentUser)
    : IRequestHandler<SetRepositoryFavoriteCommand>
{
    public async Task Handle(SetRepositoryFavoriteCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!await queries.ExistsAsync(request.OrganizationId, request.RepositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
        await favorites.SetAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, request.IsFavorite, cancellationToken);
    }
}

public sealed class GetIndexingStatusHandler(IOrganizationAccess access, IRepositoryQueries queries, ICurrentUser currentUser) : IRequestHandler<GetIndexingStatusQuery, IndexingRequestDto>
{
    public async Task<IndexingRequestDto> Handle(GetIndexingStatusQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        return await queries.GetIndexingRequestAsync(request.OrganizationId, request.RepositoryId, cancellationToken) ?? throw new NotFoundException("Repository was not found.");
    }
}

public sealed class CancelIndexingHandler(IOrganizationAccess access, IIndexingRequestRepository repository, ICurrentUser currentUser, TimeProvider timeProvider) : IRequestHandler<CancelIndexingCommand>
{
    public async Task Handle(CancelIndexingCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        var job = await repository.GetLatestAsync(request.OrganizationId, request.RepositoryId, cancellationToken);
        if (job is null || job.OrganizationId != request.OrganizationId) throw new NotFoundException("Repository was not found.");
        job.RequestCancellation(timeProvider.GetUtcNow()); await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RetryIndexingHandler(IOrganizationAccess access, IIndexingRequestRepository repository, ICurrentUser currentUser) : IRequestHandler<RetryIndexingCommand>
{
    public async Task Handle(RetryIndexingCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        var job = await repository.GetLatestAsync(request.OrganizationId, request.RepositoryId, cancellationToken);
        if (job is null || job.OrganizationId != request.OrganizationId) throw new NotFoundException("Repository was not found.");
        job.Retry(); await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ReindexRepositoryHandler(IOrganizationAccess access, IRepositoryQueries queries, IIndexingRequestRepository jobs, ICurrentUser currentUser) : IRequestHandler<ReindexRepositoryCommand>
{
    public async Task Handle(ReindexRepositoryCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!await queries.ExistsAsync(request.OrganizationId, request.RepositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
        var current = await jobs.GetLatestAsync(request.OrganizationId, request.RepositoryId, cancellationToken);
        if (current is { Status: IndexingRequestStatus.Pending or IndexingRequestStatus.Processing }) throw new ConflictException("Repository indexing is already in progress.");
        await jobs.AddAsync(new RepositoryIndexingRequest(request.OrganizationId, request.RepositoryId, currentUser.UserId), cancellationToken);
        await jobs.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RemoveRepositoryValidator : AbstractValidator<RemoveRepositoryCommand>
{
    public RemoveRepositoryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Confirmation).NotEmpty().MaximumLength(201);
    }
}

public sealed class RemoveRepositoryHandler(IOrganizationAccess access, IRepositoryRemovalStore repositories, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<RemoveRepositoryCommand>
{
    public async Task Handle(RemoveRepositoryCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Administrator, cancellationToken);
        await repositories.RemoveAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, request.Confirmation.Trim(), timeProvider.GetUtcNow(), cancellationToken);
    }
}

public sealed class ListRepositoryEndpointsHandler(IOrganizationAccess access, IRepositoryQueries queries, ICurrentUser currentUser) : IRequestHandler<ListRepositoryEndpointsQuery, IReadOnlyCollection<RepositoryEndpointDto>>
{
    public async Task<IReadOnlyCollection<RepositoryEndpointDto>> Handle(ListRepositoryEndpointsQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        return await queries.ListEndpointsAsync(request.OrganizationId, request.RepositoryId, request.Method, request.Search, request.RequiresAuthorization, cancellationToken);
    }
}

public sealed class GetRepositoryCapabilitiesHandler(IOrganizationAccess access, IRepositoryQueries queries, ICurrentUser currentUser) : IRequestHandler<GetRepositoryCapabilitiesQuery, RepositoryCapabilitiesDto>
{
    public async Task<RepositoryCapabilitiesDto> Handle(GetRepositoryCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        return await queries.GetCapabilitiesAsync(request.OrganizationId, request.RepositoryId, cancellationToken);
    }
}

public sealed class SemanticSearchHandler(IOrganizationAccess access, IRepositoryQueries repositories, IEmbeddingGenerator embeddings, IVectorStore vectors, ICurrentUser currentUser) : IRequestHandler<SemanticSearchQuery, IReadOnlyCollection<SemanticSearchResult>>
{
    public async Task<IReadOnlyCollection<SemanticSearchResult>> Handle(SemanticSearchQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!await repositories.ExistsAsync(request.OrganizationId, request.RepositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
        var query = (await embeddings.GenerateAsync([request.Query.Trim()], cancellationToken))[0];
        return await vectors.SearchAsync(request.OrganizationId, request.RepositoryId, query, request.Limit, cancellationToken);
    }
}

public sealed class StreamRepositoryChatHandler(
    IOrganizationAccess access,
    IRepositoryQueries repositories,
    IEmbeddingGenerator embeddings,
    IVectorStore vectors,
    IRepositoryAnswerGenerator answers,
    IRepositoryChatSessionStore sessions,
    ICurrentUser currentUser) : IStreamRequestHandler<StreamRepositoryChatQuery, RepositoryChatEvent>
{
    public async IAsyncEnumerable<RepositoryChatEvent> Handle(StreamRepositoryChatQuery request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var validation = await new StreamRepositoryChatValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        if (!await repositories.ExistsAsync(request.OrganizationId, request.RepositoryId, cancellationToken)) throw new NotFoundException("Repository was not found.");
        if (!answers.IsConfigured) throw new ExternalServiceException("Repository chat is not configured.");

        var question = request.Question.Trim();
        var sessionId = await sessions.StartAsync(request.OrganizationId, request.RepositoryId, currentUser.UserId, answers.Model, cancellationToken);
        var completed = false;
        try
        {
            var embedding = (await embeddings.GenerateAsync([question], cancellationToken))[0];
            var sources = await vectors.SearchAsync(request.OrganizationId, request.RepositoryId, embedding, 8, cancellationToken);
            var citations = sources.Select((source, index) => new RepositoryChatCitation(index + 1, source.Path, source.StartLine, source.EndLine, source.CommitSha, source.SourceUrl, source.Score)).ToArray();
            yield return new RepositoryChatEvent(RepositoryChatEventType.Citations, Citations: citations);

            if (sources.Count == 0)
                yield return new RepositoryChatEvent(RepositoryChatEventType.Delta, "I could not find enough indexed repository evidence to answer that question.");
            else
                await foreach (var delta in answers.StreamAsync(question, sources, cancellationToken).WithCancellation(cancellationToken))
                    if (!string.IsNullOrEmpty(delta)) yield return new RepositoryChatEvent(RepositoryChatEventType.Delta, delta);

            yield return new RepositoryChatEvent(RepositoryChatEventType.Completed);
            completed = true;
        }
        finally
        {
            var status = completed ? RepositoryChatStatus.Completed : cancellationToken.IsCancellationRequested ? RepositoryChatStatus.Cancelled : RepositoryChatStatus.Failed;
            await sessions.FinishAsync(sessionId, status, CancellationToken.None);
        }
    }
}
