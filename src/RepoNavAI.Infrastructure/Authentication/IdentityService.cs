using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RepoNavAI.Application.Authentication;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Infrastructure.Identity;

namespace RepoNavAI.Infrastructure.Authentication;

public sealed class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService
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

    private async Task<AuthenticatedUser> ToAuthenticatedUserAsync(ApplicationUser user) =>
        new(user.Id, user.Email!, user.DisplayName, (await userManager.GetRolesAsync(user)).ToArray());
}

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string User = "User";
    public static readonly string[] All = [Administrator, User];
}
