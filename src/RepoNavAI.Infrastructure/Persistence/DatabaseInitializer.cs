using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepoNavAI.Infrastructure.Authentication;
using RepoNavAI.Infrastructure.Identity;

namespace RepoNavAI.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var logger = provider.GetRequiredService<ILogger<AppDbContext>>();
        await db.Database.MigrateAsync(cancellationToken);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        var configuration = provider.GetRequiredService<IConfiguration>();
        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Administrator seed skipped because Admin credentials are not configured");
            return;
        }

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = "RepoNav Administrator", EmailConfirmed = true, CreatedAtUtc = DateTimeOffset.UtcNow };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded) throw new InvalidOperationException($"Administrator seed failed: {string.Join(", ", result.Errors.Select(x => x.Description))}");
        }
        if (!await users.IsInRoleAsync(admin, Roles.Administrator)) await users.AddToRoleAsync(admin, Roles.Administrator);
    }
}
