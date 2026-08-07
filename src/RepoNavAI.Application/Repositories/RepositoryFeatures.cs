using FluentValidation;
using MediatR;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Application.Repositories;

public sealed record RegisterRepositoryCommand(Guid OrganizationId, string Url) : IRequest<RepositoryDto>;
public sealed record ListRepositoriesQuery(Guid OrganizationId) : IRequest<IReadOnlyCollection<RepositoryDto>>;
public sealed record GetIndexingStatusQuery(Guid OrganizationId, Guid RepositoryId) : IRequest<IndexingRequestDto>;
public sealed record CancelIndexingCommand(Guid OrganizationId, Guid RepositoryId) : IRequest;
public sealed record RetryIndexingCommand(Guid OrganizationId, Guid RepositoryId) : IRequest;

public sealed class RegisterRepositoryValidator : AbstractValidator<RegisterRepositoryCommand>
{
    public RegisterRepositoryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048).Must(value => GitHubRepositoryAddress.TryParse(value, out _))
            .WithMessage("Enter a valid HTTPS GitHub repository URL, for example https://github.com/owner/repository.");
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
    : IRequestHandler<ListRepositoriesQuery, IReadOnlyCollection<RepositoryDto>>
{
    public async Task<IReadOnlyCollection<RepositoryDto>> Handle(ListRepositoriesQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Member, cancellationToken);
        return await queries.ListAsync(request.OrganizationId, cancellationToken);
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
