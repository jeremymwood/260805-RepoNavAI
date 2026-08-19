using FluentValidation;
using MediatR;
using RepoNavAI.Application.Common.Exceptions;

namespace RepoNavAI.Application.Authentication;

public sealed record AuthenticatedUser(Guid Id, string Email, string DisplayName, IReadOnlyCollection<string> Roles);
public sealed record AuthenticationResult(string AccessToken, DateTimeOffset ExpiresAtUtc, AuthenticatedUser User);
public sealed record RegisterCommand(string Email, string Password, string DisplayName) : IRequest<AuthenticationResult>;
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthenticationResult>;

public interface IIdentityService
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<AuthenticatedUser> CreateUserAsync(string email, string password, string displayName, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);
    Task<AuthenticatedUser> FindOrCreateExternalUserAsync(string provider, string providerKey, string email, bool emailVerified, string displayName, CancellationToken cancellationToken);
    Task<string> CreateExternalAuthenticationCodeAsync(Guid userId, CancellationToken cancellationToken);
    Task<AuthenticatedUser> RedeemExternalAuthenticationCodeAsync(string code, CancellationToken cancellationToken);
}

public interface ITokenService { AuthenticationResult CreateToken(AuthenticatedUser user); }

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a symbol.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RegisterCommandHandler(IIdentityService identity, ITokenService tokens) : IRequestHandler<RegisterCommand, AuthenticationResult>
{
    public async Task<AuthenticationResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await identity.EmailExistsAsync(request.Email, cancellationToken)) throw new ConflictException("An account with this email already exists.");
        return tokens.CreateToken(await identity.CreateUserAsync(request.Email, request.Password, request.DisplayName, cancellationToken));
    }
}

public sealed class LoginCommandHandler(IIdentityService identity, ITokenService tokens) : IRequestHandler<LoginCommand, AuthenticationResult>
{
    public async Task<AuthenticationResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await identity.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);
        if (user is null) throw new UnauthorizedException("Invalid email or password.");
        return tokens.CreateToken(user);
    }
}
