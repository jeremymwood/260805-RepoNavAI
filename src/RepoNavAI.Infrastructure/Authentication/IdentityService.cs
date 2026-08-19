using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using RepoNavAI.Application.Authentication;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Infrastructure.Identity;
using RepoNavAI.Infrastructure.Persistence;

namespace RepoNavAI.Infrastructure.Authentication;

public sealed class IdentityService(UserManager<ApplicationUser> userManager, AppDbContext dbContext) : IIdentityService
{
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        userManager.Users.AnyAsync(x => x.NormalizedEmail == email.ToUpperInvariant(), cancellationToken);

    public async Task<AuthenticatedUser> CreateUserAsync(string email, string password, string displayName, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email.Trim(), UserName = email.Trim(), DisplayName = displayName.Trim(), CreatedAtUtc = DateTimeOffset.UtcNow };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) throw new ConflictException(string.Join(" ", result.Errors.Select(x => x.Description)));
        await userManager.AddToRoleAsync(user, Roles.User);
        return await ToAuthenticatedUserAsync(user);
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.CheckPasswordAsync(user, password)) return null;
        return await ToAuthenticatedUserAsync(user);
    }

    public async Task<AuthenticatedUser> FindOrCreateExternalUserAsync(string provider, string providerKey, string email, bool emailVerified, string displayName, CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByLoginAsync(provider, providerKey);
        if (existing is not null) return await ToAuthenticatedUserAsync(existing);
        if (!emailVerified) throw new UnauthorizedException("The external provider did not verify this email address.");
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("An account with this email already exists. Sign in to that account before linking another provider.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), Email = email.Trim(), UserName = email.Trim(), EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName.Trim(), CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded) throw new ConflictException(string.Join(" ", created.Errors.Select(x => x.Description)));
        var linked = await userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
        if (!linked.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new ConflictException(string.Join(" ", linked.Errors.Select(x => x.Description)));
        }
        await userManager.AddToRoleAsync(user, Roles.User);
        return await ToAuthenticatedUserAsync(user);
    }

    public async Task<string> CreateExternalAuthenticationCodeAsync(Guid userId, CancellationToken cancellationToken)
    {
        await dbContext.ExternalAuthenticationCodes.Where(x => x.ExpiresAtUtc < DateTimeOffset.UtcNow || x.ConsumedAtUtc != null).ExecuteDeleteAsync(cancellationToken);
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        dbContext.ExternalAuthenticationCodes.Add(new ExternalAuthenticationCode(HashCode(code), userId, DateTimeOffset.UtcNow.AddMinutes(2)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return code;
    }

    public async Task<AuthenticatedUser> RedeemExternalAuthenticationCodeAsync(string code, CancellationToken cancellationToken)
    {
        var hash = HashCode(code);
        var ticket = await dbContext.ExternalAuthenticationCodes.AsNoTracking().SingleOrDefaultAsync(x => x.CodeHash == hash && x.ConsumedAtUtc == null && x.ExpiresAtUtc > DateTimeOffset.UtcNow, cancellationToken);
        if (ticket is null) throw new UnauthorizedException("The external sign-in code is invalid or expired.");
        var consumed = await dbContext.ExternalAuthenticationCodes.Where(x => x.Id == ticket.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ConsumedAtUtc, DateTimeOffset.UtcNow), cancellationToken);
        if (consumed != 1) throw new UnauthorizedException("The external sign-in code has already been used.");
        var user = await userManager.FindByIdAsync(ticket.UserId.ToString()) ?? throw new UnauthorizedException("The external sign-in account is unavailable.");
        return await ToAuthenticatedUserAsync(user);
    }

    private static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private async Task<AuthenticatedUser> ToAuthenticatedUserAsync(ApplicationUser user) =>
        new(user.Id, user.Email!, user.DisplayName, (await userManager.GetRolesAsync(user)).ToArray());
}

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string User = "User";
    public static readonly string[] All = [Administrator, User];
}
