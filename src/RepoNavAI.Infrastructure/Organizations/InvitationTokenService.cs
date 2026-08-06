using System.Security.Cryptography;
using System.Text;
using RepoNavAI.Application.Organizations;

namespace RepoNavAI.Infrastructure.Organizations;

public sealed class InvitationTokenService : IInvitationTokenService
{
    public string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
