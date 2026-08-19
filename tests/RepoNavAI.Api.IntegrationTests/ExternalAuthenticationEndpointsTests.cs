using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepoNavAI.Application.Authentication;
using RepoNavAI.Application.Common.Exceptions;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class ExternalAuthenticationEndpointsTests : IClassFixture<OrganizationApiFactory>
{
    private readonly HttpClient _client;
    public ExternalAuthenticationEndpointsTests(OrganizationApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Providers_WhenCredentialsAreAbsent_ReturnsDisabledOptions()
    {
        var providers = await _client.GetFromJsonAsync<ExternalProvider[]>("/api/auth/external/providers");
        providers.Should().HaveCount(3);
        providers.Should().OnlyContain(x => !x.Enabled);
        providers!.Select(x => x.Id).Should().BeEquivalentTo("Google", "Apple", "Microsoft");
    }

    [Fact]
    public async Task Challenge_ForDisabledProvider_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/auth/external/google/challenge?returnUrl=%2Frepositories");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record ExternalProvider(string Id, string DisplayName, bool Enabled);
}

public sealed class ExternalAuthenticationExchangeTests : IClassFixture<ExternalAuthenticationApiFactory>
{
    private readonly HttpClient _client;
    public ExternalAuthenticationExchangeTests(ExternalAuthenticationApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Exchange_UsesHttpOnlyStrictCookieAndDoesNotReturnJwt()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/external/exchange", new { code = "valid-code" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("test-jwt").And.NotContain("accessToken");
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        cookie.Should().Contain("RepoNavAI.Session=test-jwt");
        cookie.Should().ContainEquivalentOf("httponly").And.ContainEquivalentOf("samesite=strict");

        (await _client.PostAsJsonAsync("/api/auth/external/exchange", new { code = "valid-code" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public sealed class ExternalAuthenticationApiFactory : OrganizationApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentityService>(); services.RemoveAll<ITokenService>();
            services.AddSingleton<IIdentityService, ExternalIdentityStub>(); services.AddSingleton<ITokenService, ExternalTokenStub>();
        });
    }
}

public sealed class ExternalIdentityStub : IIdentityService
{
    private int _redeemed;
    private static readonly AuthenticatedUser User = new(Guid.Parse("20000000-0000-0000-0000-000000000002"), "external@example.com", "External User", ["User"]);
    public Task<AuthenticatedUser> RedeemExternalAuthenticationCodeAsync(string code, CancellationToken cancellationToken) =>
        code == "valid-code" && Interlocked.Exchange(ref _redeemed, 1) == 0 ? Task.FromResult(User) : throw new UnauthorizedException("The external sign-in code is invalid or expired.");
    public Task<string> CreateExternalAuthenticationCodeAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult("valid-code");
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<AuthenticatedUser> CreateUserAsync(string email, string password, string displayName, CancellationToken cancellationToken) => Task.FromResult(User);
    public Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken) => Task.FromResult<AuthenticatedUser?>(User);
    public Task<AuthenticatedUser> FindOrCreateExternalUserAsync(string provider, string providerKey, string email, bool emailVerified, string displayName, CancellationToken cancellationToken) => Task.FromResult(User);
}

public sealed class ExternalTokenStub : ITokenService
{
    public AuthenticationResult CreateToken(AuthenticatedUser user) => new("test-jwt", DateTimeOffset.UtcNow.AddMinutes(5), user);
}
