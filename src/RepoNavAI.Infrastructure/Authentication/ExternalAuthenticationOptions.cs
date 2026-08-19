namespace RepoNavAI.Infrastructure.Authentication;

public sealed class ExternalAuthenticationOptions
{
    public const string SectionName = "Authentication";
    public string FrontendUrl { get; init; } = "http://localhost:5173";
    public ExternalProviderOptions Google { get; init; } = new();
    public ExternalProviderOptions Apple { get; init; } = new();
    public ExternalProviderOptions Microsoft { get; init; } = new();
}

public sealed class ExternalProviderOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public bool Enabled => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public static class ExternalAuthenticationSchemes
{
    public const string SessionCookie = "RepoNavAI.Session";
    public const string Cookie = "ExternalCookie";
    public const string Google = "Google";
    public const string Apple = "Apple";
    public const string Microsoft = "Microsoft";
    public static readonly string[] Supported = [Google, Apple, Microsoft];
}
