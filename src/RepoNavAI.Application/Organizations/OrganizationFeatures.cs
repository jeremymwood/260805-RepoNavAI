using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Domain.Organizations;

namespace RepoNavAI.Application.Organizations;

public sealed record CreateOrganizationCommand(string Name) : IRequest<OrganizationSummary>;
public sealed record ListOrganizationsQuery : IRequest<IReadOnlyCollection<OrganizationSummary>>;
public sealed record GetOrganizationQuery(Guid OrganizationId) : IRequest<OrganizationDetails>;
public sealed record RenameOrganizationCommand(Guid OrganizationId, string Name) : IRequest;
public sealed record InviteOrganizationMemberCommand(Guid OrganizationId, string Email, OrganizationRole Role) : IRequest<InvitationResult>;
public sealed record ListPendingOrganizationInvitationsQuery(Guid OrganizationId) : IRequest<IReadOnlyCollection<PendingInvitationDto>>;
public sealed record RevokeOrganizationInvitationCommand(Guid OrganizationId, Guid InvitationId) : IRequest;
public sealed record AcceptOrganizationInvitationCommand(string Token) : IRequest<OrganizationSummary>;
public sealed record ChangeOrganizationMemberRoleCommand(Guid OrganizationId, Guid UserId, OrganizationRole Role) : IRequest;
public sealed record RemoveOrganizationMemberCommand(Guid OrganizationId, Guid UserId) : IRequest;

public sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}
public sealed class RenameOrganizationValidator : AbstractValidator<RenameOrganizationCommand>
{
    public RenameOrganizationValidator() { RuleFor(x => x.OrganizationId).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(150); }
}
public sealed class InviteOrganizationMemberValidator : AbstractValidator<InviteOrganizationMemberCommand>
{
    public InviteOrganizationMemberValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum().Must(x => x != OrganizationRole.Owner).WithMessage("Owner access cannot be granted through an invitation.");
    }
}
public sealed class AcceptOrganizationInvitationValidator : AbstractValidator<AcceptOrganizationInvitationCommand>
{
    public AcceptOrganizationInvitationValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
}
public sealed class ChangeOrganizationMemberRoleValidator : AbstractValidator<ChangeOrganizationMemberRoleCommand>
{
    public ChangeOrganizationMemberRoleValidator() { RuleFor(x => x.OrganizationId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.Role).IsInEnum(); }
}

public sealed class CreateOrganizationHandler(IOrganizationRepository repository, ICurrentUser currentUser) : IRequestHandler<CreateOrganizationCommand, OrganizationSummary>
{
    public async Task<OrganizationSummary> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var baseSlug = Regex.Replace(request.Name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "organization";
        var slug = baseSlug;
        for (var suffix = 2; await repository.SlugExistsAsync(slug, cancellationToken); suffix++) slug = $"{baseSlug}-{suffix}";
        var organization = new Organization(request.Name, slug);
        organization.AddMember(currentUser.UserId, OrganizationRole.Owner);
        await repository.AddAsync(organization, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new OrganizationSummary(organization.Id, organization.Name, organization.Slug, OrganizationRole.Owner);
    }
}

public sealed class ListOrganizationsHandler(IOrganizationQueries queries, ICurrentUser currentUser) : IRequestHandler<ListOrganizationsQuery, IReadOnlyCollection<OrganizationSummary>>
{
    public Task<IReadOnlyCollection<OrganizationSummary>> Handle(ListOrganizationsQuery request, CancellationToken cancellationToken) => queries.ListForUserAsync(currentUser.UserId, cancellationToken);
}

public sealed class GetOrganizationHandler(IOrganizationQueries queries, ICurrentUser currentUser) : IRequestHandler<GetOrganizationQuery, OrganizationDetails>
{
    public async Task<OrganizationDetails> Handle(GetOrganizationQuery request, CancellationToken cancellationToken) =>
        await queries.GetForUserAsync(request.OrganizationId, currentUser.UserId, cancellationToken) ?? throw new NotFoundException("Organization was not found.");
}

public sealed class RenameOrganizationHandler(IOrganizationAccess access, IOrganizationRepository repository, ICurrentUser currentUser) : IRequestHandler<RenameOrganizationCommand>
{
    public async Task Handle(RenameOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Administrator, cancellationToken);
        organization.Rename(request.Name);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class InviteOrganizationMemberHandler(IOrganizationAccess access, IOrganizationRepository repository, IInvitationTokenService tokens, ICurrentUser currentUser, TimeProvider timeProvider) : IRequestHandler<InviteOrganizationMemberCommand, InvitationResult>
{
    public async Task<InvitationResult> Handle(InviteOrganizationMemberCommand request, CancellationToken cancellationToken)
    {
        var organization = await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Administrator, cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        if (await repository.IsEmailMemberAsync(organization.Id, email, cancellationToken)) throw new ConflictException("User is already an organization member.");
        if (await repository.HasPendingInvitationAsync(organization.Id, email, cancellationToken)) throw new ConflictException("A pending invitation already exists for this email.");
        var token = tokens.CreateToken();
        var expires = timeProvider.GetUtcNow().AddDays(7);
        var invitation = new OrganizationInvitation(organization.Id, email, request.Role, tokens.HashToken(token), currentUser.UserId, expires);
        await repository.AddInvitationAsync(invitation, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new InvitationResult(invitation.Id, token, expires);
    }
}

public sealed class AcceptOrganizationInvitationHandler(IOrganizationRepository repository, IOrganizationQueries queries, IInvitationTokenService tokens, ICurrentUser currentUser, TimeProvider timeProvider) : IRequestHandler<AcceptOrganizationInvitationCommand, OrganizationSummary>
{
    public async Task<OrganizationSummary> Handle(AcceptOrganizationInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await repository.GetInvitationByTokenHashAsync(tokens.HashToken(request.Token), cancellationToken) ?? throw new NotFoundException("Invitation was not found.");
        if (!string.Equals(invitation.Email, currentUser.Email, StringComparison.OrdinalIgnoreCase)) throw new ForbiddenException("This invitation belongs to a different account.");
        if (!invitation.IsPending(timeProvider.GetUtcNow())) throw new ConflictException("Invitation is no longer valid.");
        var organization = await repository.GetWithMembersAsync(invitation.OrganizationId, cancellationToken) ?? throw new NotFoundException("Organization was not found.");
        if (organization.Members.All(x => x.UserId != currentUser.UserId)) organization.AddMember(currentUser.UserId, invitation.Role);
        invitation.Accept(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return (await queries.ListForUserAsync(currentUser.UserId, cancellationToken)).Single(x => x.Id == organization.Id);
    }
}

public sealed class ListPendingOrganizationInvitationsHandler(IOrganizationAccess access, IOrganizationQueries queries, ICurrentUser currentUser, TimeProvider timeProvider) : IRequestHandler<ListPendingOrganizationInvitationsQuery, IReadOnlyCollection<PendingInvitationDto>>
{
    public async Task<IReadOnlyCollection<PendingInvitationDto>> Handle(ListPendingOrganizationInvitationsQuery request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Administrator, cancellationToken);
        return await queries.ListPendingInvitationsAsync(request.OrganizationId, timeProvider.GetUtcNow(), cancellationToken);
    }
}

public sealed class RevokeOrganizationInvitationHandler(IOrganizationAccess access, IOrganizationRepository repository, ICurrentUser currentUser, TimeProvider timeProvider) : IRequestHandler<RevokeOrganizationInvitationCommand>
{
    public async Task Handle(RevokeOrganizationInvitationCommand request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Administrator, cancellationToken);
        var invitation = await repository.GetInvitationByIdAsync(request.InvitationId, cancellationToken);
        if (invitation is null || invitation.OrganizationId != request.OrganizationId) throw new NotFoundException("Invitation was not found.");
        if (!invitation.IsPending(timeProvider.GetUtcNow())) throw new ConflictException("Invitation is no longer pending.");
        invitation.Revoke(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ChangeOrganizationMemberRoleHandler(IOrganizationAccess access, IOrganizationRepository repository, ICurrentUser currentUser) : IRequestHandler<ChangeOrganizationMemberRoleCommand>
{
    public async Task Handle(ChangeOrganizationMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var organization = await access.RequireAsync(request.OrganizationId, currentUser.UserId, OrganizationRole.Owner, cancellationToken);
        organization.ChangeMemberRole(request.UserId, request.Role);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RemoveOrganizationMemberHandler(IOrganizationAccess access, IOrganizationRepository repository, ICurrentUser currentUser) : IRequestHandler<RemoveOrganizationMemberCommand>
{
    public async Task Handle(RemoveOrganizationMemberCommand request, CancellationToken cancellationToken)
    {
        var requiredRole = request.UserId == currentUser.UserId ? OrganizationRole.Member : OrganizationRole.Administrator;
        var organization = await access.RequireAsync(request.OrganizationId, currentUser.UserId, requiredRole, cancellationToken);
        organization.RemoveMember(request.UserId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
