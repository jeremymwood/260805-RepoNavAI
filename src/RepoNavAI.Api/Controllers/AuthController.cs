using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RepoNavAI.Application.Authentication;
using RepoNavAI.Infrastructure.Authentication;
using System.Security.Claims;

namespace RepoNavAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender, IIdentityService identity, ITokenService tokens, IOptions<ExternalAuthenticationOptions> externalOptions) : ControllerBase
{
    private readonly ExternalAuthenticationOptions _external = externalOptions.Value;
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticatedSessionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthenticatedSessionDto>> Register(RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        SetSessionCookie(result);
        return StatusCode(StatusCodes.Status201Created, ToSession(result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticatedSessionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticatedSessionDto>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        SetSessionCookie(result);
        return Ok(ToSession(result));
    }

    [HttpPost("logout")]
    public IActionResult Logout([FromHeader(Name = "X-RepoNavAI-Logout")] string? intent)
    {
        // Ignore logout calls from stale clients that used to react to any 401 by
        // clearing the shared browser cookie. Current clients mark user-initiated logout.
        if (!string.Equals(intent, "explicit", StringComparison.Ordinal)) return NoContent();
        Response.Cookies.Delete(ExternalAuthenticationSchemes.SessionCookie, SessionCookieOptions(DateTimeOffset.UtcNow));
        return NoContent();
    }

    [HttpGet("external/providers")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyCollection<ExternalProviderDto>> Providers() => Ok(new[]
    {
        new ExternalProviderDto(ExternalAuthenticationSchemes.Google, "Google", _external.Google.Enabled),
        new ExternalProviderDto(ExternalAuthenticationSchemes.Apple, "Apple", _external.Apple.Enabled),
        new ExternalProviderDto(ExternalAuthenticationSchemes.Microsoft, "Microsoft", _external.Microsoft.Enabled)
    });

    [HttpGet("external/{provider}/challenge")]
    [AllowAnonymous]
    public ActionResult ExternalChallenge(string provider, [FromQuery] string? returnUrl)
    {
        var scheme = NormalizeEnabledProvider(provider);
        var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(ExternalCallback)) };
        properties.Items["provider"] = scheme;
        properties.Items["returnUrl"] = NormalizeReturnUrl(returnUrl);
        return Challenge(properties, scheme);
    }

    [HttpGet("external/callback")]
    [AllowAnonymous]
    public async Task<ActionResult> ExternalCallback(CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(ExternalAuthenticationSchemes.Cookie);
        var returnUrl = ReadProperty(result.Properties, "returnUrl") ?? "/";
        try
        {
            if (!result.Succeeded || result.Principal is null) return RedirectToFrontend(returnUrl, null, "External sign-in could not be completed.");
            var provider = ReadProperty(result.Properties, "provider");
            var providerKey = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? result.Principal.FindFirstValue("sub");
            var email = result.Principal.FindFirstValue(ClaimTypes.Email) ?? result.Principal.FindFirstValue("email") ?? result.Principal.FindFirstValue("preferred_username");
            var name = result.Principal.FindFirstValue(ClaimTypes.Name) ?? result.Principal.FindFirstValue("name") ?? string.Empty;
            var verifiedClaim = result.Principal.FindFirstValue("email_verified");
            var emailVerified = provider is ExternalAuthenticationSchemes.Google or ExternalAuthenticationSchemes.Microsoft || bool.TryParse(verifiedClaim, out var verified) && verified;
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email))
                return RedirectToFrontend(returnUrl, null, "The provider did not return the identity information RepoNavAI requires.");

            var user = await identity.FindOrCreateExternalUserAsync(provider, providerKey, email, emailVerified, name, cancellationToken);
            var code = await identity.CreateExternalAuthenticationCodeAsync(user.Id, cancellationToken);
            return RedirectToFrontend(returnUrl, code, null);
        }
        catch (Exception exception) when (exception is RepoNavAI.Application.Common.Exceptions.ConflictException or RepoNavAI.Application.Common.Exceptions.UnauthorizedException)
        {
            return RedirectToFrontend(returnUrl, null, exception.Message);
        }
        finally
        {
            await HttpContext.SignOutAsync(ExternalAuthenticationSchemes.Cookie);
        }
    }

    [HttpPost("external/exchange")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthenticatedSessionDto>> ExchangeExternalCode(ExternalCodeExchangeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 128) return Unauthorized();
        var user = await identity.RedeemExternalAuthenticationCodeAsync(request.Code, cancellationToken);
        var authentication = tokens.CreateToken(user);
        SetSessionCookie(authentication);
        return Ok(ToSession(authentication));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me() => Ok(new
    {
        Id = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
        Email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value,
        DisplayName = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name)?.Value,
        Roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(x => x.Value)
    });

    private string NormalizeEnabledProvider(string provider)
    {
        var scheme = ExternalAuthenticationSchemes.Supported.FirstOrDefault(x => string.Equals(x, provider, StringComparison.OrdinalIgnoreCase));
        var enabled = scheme switch
        {
            ExternalAuthenticationSchemes.Google => _external.Google.Enabled,
            ExternalAuthenticationSchemes.Apple => _external.Apple.Enabled,
            ExternalAuthenticationSchemes.Microsoft => _external.Microsoft.Enabled,
            _ => false
        };
        return enabled ? scheme! : throw new RepoNavAI.Application.Common.Exceptions.NotFoundException("The requested external authentication provider is unavailable.");
    }

    private static string NormalizeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.Contains('\\') && !returnUrl.Any(char.IsControl) ? returnUrl : "/";

    private static string? ReadProperty(AuthenticationProperties? properties, string key) =>
        properties?.Items.TryGetValue(key, out var value) == true ? value : null;

    private RedirectResult RedirectToFrontend(string returnUrl, string? code, string? error)
    {
        var values = code is not null
            ? new Dictionary<string, string> { ["code"] = code, ["return_url"] = returnUrl }
            : new Dictionary<string, string> { ["error"] = error ?? "External sign-in failed.", ["return_url"] = returnUrl };
        var fragment = string.Join('&', values.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        return Redirect($"{_external.FrontendUrl.TrimEnd('/')}/auth/callback#{fragment}");
    }

    private void SetSessionCookie(AuthenticationResult authentication) =>
        Response.Cookies.Append(ExternalAuthenticationSchemes.SessionCookie, authentication.AccessToken, SessionCookieOptions(authentication.ExpiresAtUtc));

    private CookieOptions SessionCookieOptions(DateTimeOffset expiresAtUtc) => new()
    {
        HttpOnly = true, Secure = Uri.TryCreate(_external.FrontendUrl, UriKind.Absolute, out var frontend) && frontend.Scheme == Uri.UriSchemeHttps,
        SameSite = SameSiteMode.Strict, Path = "/", Expires = expiresAtUtc, IsEssential = true
    };

    private static AuthenticatedSessionDto ToSession(AuthenticationResult authentication) => new(authentication.ExpiresAtUtc, authentication.User);
}

public sealed record ExternalProviderDto(string Id, string DisplayName, bool Enabled);
public sealed record ExternalCodeExchangeRequest(string Code);
public sealed record AuthenticatedSessionDto(DateTimeOffset ExpiresAtUtc, AuthenticatedUser User);
