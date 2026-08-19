namespace RepoNavAI.Infrastructure.Authentication;

public sealed class ExternalAuthenticationCode
{
    private ExternalAuthenticationCode() { }
    public ExternalAuthenticationCode(string codeHash, Guid userId, DateTimeOffset expiresAtUtc)
    {
        Id = Guid.NewGuid(); CodeHash = codeHash; UserId = userId; ExpiresAtUtc = expiresAtUtc; CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
}
