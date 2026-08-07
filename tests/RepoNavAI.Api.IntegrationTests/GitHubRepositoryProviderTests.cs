using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class GitHubRepositoryProviderTests
{
    [Fact]
    public async Task GetAsync_MapsVerifiedRepositoryWithoutExposingCredential()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":42,"name":"RepoNavAI","default_branch":"main","private":true,"html_url":"https://github.com/acme/RepoNavAI","owner":{"login":"acme"}}""", System.Text.Encoding.UTF8, "application/json")
        });
        var provider = new GitHubRepositoryProvider(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") }, Options.Create(new GitHubOptions { AccessToken = "secret-token" }));

        var result = await provider.GetAsync(new GitHubRepositoryAddress("acme", "reponavai"), CancellationToken.None);

        result.Should().Be(new ProviderRepository("42", "acme", "RepoNavAI", "main", RepositoryVisibility.Private, "https://github.com/acme/RepoNavAI"));
        handler.AuthorizationScheme.Should().Be("Bearer");
        handler.AuthorizationParameter.Should().Be("secret-token");
    }

    [Fact]
    public async Task GetAsync_WhenGitHubReturnsNotFound_ReturnsNull()
    {
        var provider = new GitHubRepositoryProvider(new HttpClient(new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NotFound))) { BaseAddress = new Uri("https://api.github.com/") }, Options.Create(new GitHubOptions()));
        (await provider.GetAsync(new GitHubRepositoryAddress("acme", "missing"), CancellationToken.None)).Should().BeNull();
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(response);
        }
    }
}
