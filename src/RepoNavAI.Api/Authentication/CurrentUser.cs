using System.IdentityModel.Tokens.Jwt;
using RepoNavAI.Application.Common.Identity;

namespace RepoNavAI.Api.Authentication;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal Principal => accessor.HttpContext?.User ?? throw new InvalidOperationException("No active HTTP request.");
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public Guid UserId => Guid.TryParse(Principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : throw new InvalidOperationException("Authenticated user identifier is unavailable.");
    public string Email => Principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? throw new InvalidOperationException("Authenticated user email is unavailable.");
}
