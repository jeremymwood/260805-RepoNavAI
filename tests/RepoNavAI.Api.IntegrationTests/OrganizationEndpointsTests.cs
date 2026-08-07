using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class OrganizationEndpointsTests : IClassFixture<OrganizationApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    public OrganizationEndpointsTests(OrganizationApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task CreateOrganization_MakesCurrentUserOwner()
    {
        var response = await _client.PostAsJsonAsync("/api/organizations", new { name = "Acme Engineering" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var organization = await response.Content.ReadFromJsonAsync<OrganizationSummary>(JsonOptions);
        organization.Should().NotBeNull();
        organization!.Name.Should().Be("Acme Engineering");
        organization.Role.Should().Be(OrganizationRole.Owner);
    }

    [Fact]
    public async Task GetOrganization_FromAnotherTenant_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/organizations/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameOrganization_WhenCurrentUserIsNotMember_ReturnsNotFound()
    {
        var response = await _client.PatchAsJsonAsync($"/api/organizations/{Guid.NewGuid()}", new { name = "Unauthorized rename" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListPendingInvitations_WhenCurrentUserIsNotMember_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/organizations/{Guid.NewGuid()}/invitations");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegisterRepository_WhenCurrentUserIsNotMember_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync($"/api/organizations/{Guid.NewGuid()}/repositories", new { url = "https://github.com/openai/openai-dotnet" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetIndexingStatus_WhenCurrentUserIsNotMember_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/organizations/{Guid.NewGuid()}/repositories/{Guid.NewGuid()}/indexing");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListRepositoryEndpoints_WhenCurrentUserIsNotMember_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/organizations/{Guid.NewGuid()}/repositories/{Guid.NewGuid()}/endpoints?method=GET&search=orders&requiresAuthorization=true");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListRepositoryEndpoints_ForOrganizationMember_AcceptsFilters()
    {
        var organizationResponse = await _client.PostAsJsonAsync("/api/organizations", new { name = $"Endpoint Test {Guid.NewGuid():N}" });
        var organization = await organizationResponse.Content.ReadFromJsonAsync<OrganizationSummary>(JsonOptions);
        var repositoryResponse = await _client.PostAsJsonAsync($"/api/organizations/{organization!.Id}/repositories", new { url = "https://github.com/acme/api" });
        var repository = await repositoryResponse.Content.ReadFromJsonAsync<RepositoryDto>(JsonOptions);

        var response = await _client.GetAsync($"/api/organizations/{organization.Id}/repositories/{repository!.Id}/endpoints?method=GET&search=orders&requiresAuthorization=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterRepository_AsOrganizationMember_CreatesPendingIndexingRequest()
    {
        var organizationResponse = await _client.PostAsJsonAsync("/api/organizations", new { name = $"Repository Test {Guid.NewGuid():N}" });
        var organization = await organizationResponse.Content.ReadFromJsonAsync<OrganizationSummary>(JsonOptions);

        var response = await _client.PostAsJsonAsync($"/api/organizations/{organization!.Id}/repositories", new { url = "https://github.com/acme/platform" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var repository = await response.Content.ReadFromJsonAsync<RepositoryDto>(JsonOptions);
        repository!.FullName.Should().Be("acme/platform");
        repository.IndexingStatus.Should().Be(IndexingRequestStatus.Pending);
    }
}

public sealed class OrganizationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "TEST-ONLY-SIGNING-KEY-32-CHARACTERS-MINIMUM",
            ["Indexing:WorkerEnabled"] = "false"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOrganizationRepository>();
            services.RemoveAll<IOrganizationQueries>();
            services.RemoveAll<IRepositoryRegistrationRepository>();
            services.RemoveAll<IRepositoryQueries>();
            services.RemoveAll<IRepositoryProvider>();
            services.AddSingleton<TestOrganizationStore>();
            services.AddSingleton<IOrganizationRepository>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IOrganizationQueries>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IRepositoryRegistrationRepository>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IRepositoryQueries>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IRepositoryProvider, TestRepositoryProvider>();
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme; options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme; })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.AuthenticationScheme, _ => { });
        });
    }
}

public sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "IntegrationTest";
    public static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims = [new("sub", UserId.ToString()), new("email", "integration@example.com"), new("name", "Integration User")];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationScheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
    }
}

public sealed class TestOrganizationStore : IOrganizationRepository, IOrganizationQueries, IRepositoryRegistrationRepository, IRepositoryQueries
{
    private readonly List<Organization> _organizations = [];
    private readonly List<(RegisteredRepository Repository, RepositoryIndexingRequest Request)> _repositories = [];
    public Task AddAsync(Organization organization, CancellationToken cancellationToken) { _organizations.Add(organization); return Task.CompletedTask; }
    public Task<Organization?> GetWithMembersAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult(_organizations.SingleOrDefault(x => x.Id == organizationId));
    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) => Task.FromResult(_organizations.Any(x => x.Slug == slug));
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
    public Task<OrganizationInvitation?> GetInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
    public Task<bool> HasPendingInvitationAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> IsEmailMemberAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<IReadOnlyCollection<OrganizationSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<OrganizationSummary>>(_organizations.Where(x => x.Members.Any(m => m.UserId == userId)).Select(x => new OrganizationSummary(x.Id, x.Name, x.Slug, x.Members.Single(m => m.UserId == userId).Role)).ToArray());
    public Task<OrganizationDetails?> GetForUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult<OrganizationDetails?>(null);
    public Task<IReadOnlyCollection<PendingInvitationDto>> ListPendingInvitationsAsync(Guid organizationId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<PendingInvitationDto>>([]);
    public Task<bool> ExistsAsync(Guid organizationId, string owner, string name, CancellationToken cancellationToken) => Task.FromResult(_repositories.Any(x => x.Repository.OrganizationId == organizationId && x.Repository.Owner == owner && x.Repository.Name == name));
    public Task AddAsync(RegisteredRepository repository, RepositoryIndexingRequest indexingRequest, CancellationToken cancellationToken) { _repositories.Add((repository, indexingRequest)); return Task.CompletedTask; }
    public Task<IReadOnlyCollection<RepositoryDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<RepositoryDto>>(_repositories.Where(x => x.Repository.OrganizationId == organizationId).Select(x => new RepositoryDto(x.Repository.Id, x.Repository.OrganizationId, x.Repository.Owner, x.Repository.Name, x.Repository.FullName, x.Repository.DefaultBranch, x.Repository.Visibility, x.Repository.WebUrl, x.Request.Status, x.Repository.CreatedAtUtc)).ToArray());
    public Task<IndexingRequestDto?> GetIndexingRequestAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) => Task.FromResult(_repositories.Where(x => x.Repository.OrganizationId == organizationId && x.Repository.Id == repositoryId).Select(x => new IndexingRequestDto(x.Request.Id, x.Request.RepositoryId, x.Request.Status, x.Request.Checkpoint, x.Request.AttemptCount, x.Request.CommitSha, x.Request.ErrorCode, x.Request.ErrorMessage, x.Request.CreatedAtUtc, x.Request.CompletedAtUtc)).FirstOrDefault());
    public Task<IReadOnlyCollection<RepositoryEndpointDto>> ListEndpointsAsync(Guid organizationId, Guid repositoryId, string? method, string? search, bool? requiresAuthorization, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<RepositoryEndpointDto>>([]);
}

public sealed class TestRepositoryProvider : IRepositoryProvider
{
    public Task<ProviderRepository?> GetAsync(GitHubRepositoryAddress address, CancellationToken cancellationToken) =>
        Task.FromResult<ProviderRepository?>(new ProviderRepository("123", address.Owner, address.Name, "main", RepositoryVisibility.Private, $"https://github.com/{address.Owner}/{address.Name}"));
}
