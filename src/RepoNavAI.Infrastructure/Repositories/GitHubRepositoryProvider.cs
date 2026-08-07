using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;

namespace RepoNavAI.Infrastructure.Repositories;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";
    public string AccessToken { get; init; } = string.Empty;
}

public sealed class GitHubRepositoryProvider(HttpClient httpClient, IOptions<GitHubOptions> options) : IRepositoryProvider
{
    public async Task<ProviderRepository?> GetAsync(GitHubRepositoryAddress address, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{Uri.EscapeDataString(address.Owner)}/{Uri.EscapeDataString(address.Name)}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(options.Value.AccessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ExternalServiceException("GitHub repository access could not be verified. Check the configured GitHub integration permissions.");
        if (!response.IsSuccessStatusCode) throw new ExternalServiceException("GitHub repository verification is temporarily unavailable.");
        var result = await response.Content.ReadFromJsonAsync<GitHubRepositoryResponse>(cancellationToken) ?? throw new ExternalServiceException("GitHub returned an invalid repository response.");
        return new ProviderRepository(result.Id.ToString(), result.Owner.Login, result.Name, result.DefaultBranch, result.Private ? RepositoryVisibility.Private : RepositoryVisibility.Public, result.HtmlUrl);
    }

    private sealed record GitHubRepositoryResponse(long Id, string Name, [property: JsonPropertyName("default_branch")] string DefaultBranch, bool Private, [property: JsonPropertyName("html_url")] string HtmlUrl, GitHubOwner Owner);
    private sealed record GitHubOwner(string Login);
}
