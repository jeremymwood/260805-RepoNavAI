using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoNavAI.Application.Authentication;

namespace RepoNavAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthenticationResult>> Register(RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResult>(StatusCodes.Status200OK)]
    public Task<AuthenticationResult> Login(LoginCommand command, CancellationToken cancellationToken) => sender.Send(command, cancellationToken);

    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me() => Ok(new
    {
        Id = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
        Email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value,
        DisplayName = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name)?.Value,
        Roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(x => x.Value)
    });
}
