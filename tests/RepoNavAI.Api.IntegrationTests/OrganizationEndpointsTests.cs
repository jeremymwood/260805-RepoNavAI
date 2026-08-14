using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Runtime.CompilerServices;
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
    public async Task RepositoryList_IsPagedAndOrdersCurrentUsersFavoritesFirst()
    {
        var organizationResponse = await _client.PostAsJsonAsync("/api/organizations", new { name = $"Favorites {Guid.NewGuid():N}" });
        var organization = await organizationResponse.Content.ReadFromJsonAsync<OrganizationSummary>(JsonOptions);
        var repositories = new List<RepositoryDto>();
        for (var index = 0; index < 11; index++)
        {
            var response = await _client.PostAsJsonAsync($"/api/organizations/{organization!.Id}/repositories", new { url = $"https://github.com/acme/repository-{index:00}" });
            repositories.Add((await response.Content.ReadFromJsonAsync<RepositoryDto>(JsonOptions))!);
        }

        var favorite = repositories[^1];
        var favoriteResponse = await _client.PutAsJsonAsync($"/api/organizations/{organization!.Id}/repositories/{favorite.Id}/favorite", new { isFavorite = true });
        favoriteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var page = await _client.GetFromJsonAsync<RepositoryPage>($"/api/organizations/{organization.Id}/repositories?page=1&pageSize=10", JsonOptions);
        page!.TotalCount.Should().Be(11);
        page.HasMore.Should().BeTrue();
        page.Items.Should().HaveCount(10);
        page.Items.First().Id.Should().Be(favorite.Id);
        page.Items.First().IsFavorite.Should().BeTrue();
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
    public async Task SemanticSearch_WhenCurrentUserIsNotMember_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/organizations/{Guid.NewGuid()}/repositories/{Guid.NewGuid()}/semantic-search?query=authentication");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RepositoryChat_WhenCurrentUserIsNotMember_ReturnsNotFoundBeforeStreamingStarts()
    {
        var response = await _client.PostAsJsonAsync($"/api/organizations/{Guid.NewGuid()}/repositories/{Guid.NewGuid()}/chat", new { question = "How does authentication work?" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task RepositoryChat_ForOrganizationMember_StreamsGroundedEventsAndCitations()
    {
        var organizationResponse = await _client.PostAsJsonAsync("/api/organizations", new { name = $"Chat Test {Guid.NewGuid():N}" });
        var organization = await organizationResponse.Content.ReadFromJsonAsync<OrganizationSummary>(JsonOptions);
        var repositoryResponse = await _client.PostAsJsonAsync($"/api/organizations/{organization!.Id}/repositories", new { url = "https://github.com/acme/chat" });
        var repository = await repositoryResponse.Content.ReadFromJsonAsync<RepositoryDto>(JsonOptions);

        var response = await _client.PostAsJsonAsync($"/api/organizations/{organization.Id}/repositories/{repository!.Id}/chat", new { question = "How does authentication work?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("event: citations").And.Contain("src/Auth.cs").And.Contain("event: delta").And.Contain("Authentication is enforced [1].").And.Contain("event: completed");
    }

    [Fact]
    public async Task ReindexRepository_WhenCurrentUserIsNotMember_ReturnsNotFound()
    {
        var response = await _client.PostAsync($"/api/organizations/{Guid.NewGuid()}/repositories/{Guid.NewGuid()}/indexing/reindex", null);
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

    [Fact]
    public async Task RemoveRepository_WithExactConfirmation_RemovesItAndAllowsReregistration()
    {
        var organizationResponse = await _client.PostAsJsonAsync("/api/organizations", new { name = $"Removal {Guid.NewGuid():N}" });
        var organization = await organizationResponse.Content.ReadFromJsonAsync<OrganizationSummary>(JsonOptions);
        var registeredResponse = await _client.PostAsJsonAsync($"/api/organizations/{organization!.Id}/repositories", new { url = "https://github.com/acme/removable" });
        var repository = await registeredResponse.Content.ReadFromJsonAsync<RepositoryDto>(JsonOptions);

        var removal = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/organizations/{organization.Id}/repositories/{repository!.Id}") { Content = JsonContent.Create(new { confirmation = repository.FullName }) });

        removal.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetFromJsonAsync<RepositoryPage>($"/api/organizations/{organization.Id}/repositories", JsonOptions))!.Items.Should().BeEmpty();
        (await _client.PostAsJsonAsync($"/api/organizations/{organization.Id}/repositories", new { url = "https://github.com/acme/removable" })).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RemoveRepository_WithWrongConfirmation_ReturnsBadRequestAndKeepsRepository()
    {
        var organizationResponse = await _client.PostAsJsonAsync("/api/organizations", new { name = $"Removal confirmation {Guid.NewGuid():N}" });
        var organization = await organizationResponse.Content.ReadFromJsonAsync<OrganizationSummary>(JsonOptions);
        var registeredResponse = await _client.PostAsJsonAsync($"/api/organizations/{organization!.Id}/repositories", new { url = "https://github.com/acme/keep-me" });
        var repository = await registeredResponse.Content.ReadFromJsonAsync<RepositoryDto>(JsonOptions);

        var removal = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/organizations/{organization.Id}/repositories/{repository!.Id}") { Content = JsonContent.Create(new { confirmation = "wrong/name" }) });

        removal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetFromJsonAsync<RepositoryPage>($"/api/organizations/{organization.Id}/repositories", JsonOptions))!.Items.Should().ContainSingle(x => x.Id == repository.Id);
    }

    [Fact]
    public async Task RemoveRepository_FromAnotherTenant_ReturnsNotFound()
    {
        var removal = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/organizations/{Guid.NewGuid()}/repositories/{Guid.NewGuid()}") { Content = JsonContent.Create(new { confirmation = "acme/repository" }) });
        removal.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
            services.RemoveAll<IRepositoryFavoriteStore>();
            services.RemoveAll<IRepositoryRemovalStore>();
            services.RemoveAll<IRepositoryProvider>();
            services.RemoveAll<IEmbeddingGenerator>();
            services.RemoveAll<IVectorStore>();
            services.RemoveAll<IRepositoryAnswerGenerator>();
            services.RemoveAll<IRepositoryChatSessionStore>();
            services.AddSingleton<TestOrganizationStore>();
            services.AddSingleton<IOrganizationRepository>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IOrganizationQueries>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IRepositoryRegistrationRepository>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IRepositoryQueries>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IRepositoryFavoriteStore>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IRepositoryRemovalStore>(provider => provider.GetRequiredService<TestOrganizationStore>());
            services.AddSingleton<IRepositoryProvider, TestRepositoryProvider>();
            services.AddSingleton<IEmbeddingGenerator, TestEmbeddingGenerator>();
            services.AddSingleton<IVectorStore, TestVectorStore>();
            services.AddSingleton<IRepositoryAnswerGenerator, TestRepositoryAnswerGenerator>();
            services.AddSingleton<IRepositoryChatSessionStore, TestRepositoryChatSessionStore>();
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

public sealed class TestOrganizationStore : IOrganizationRepository, IOrganizationQueries, IRepositoryRegistrationRepository, IRepositoryQueries, IRepositoryFavoriteStore, IRepositoryRemovalStore
{
    private readonly List<Organization> _organizations = [];
    private readonly List<(RegisteredRepository Repository, RepositoryIndexingRequest Request)> _repositories = [];
    private readonly HashSet<(Guid OrganizationId, Guid UserId, Guid RepositoryId)> _favorites = [];
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
    public Task<bool> ExistsAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) => Task.FromResult(_repositories.Any(x => x.Repository.OrganizationId == organizationId && x.Repository.Id == repositoryId));
    public Task AddAsync(RegisteredRepository repository, RepositoryIndexingRequest indexingRequest, CancellationToken cancellationToken) { _repositories.Add((repository, indexingRequest)); return Task.CompletedTask; }
    public Task<RepositoryPage> ListAsync(Guid organizationId, Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var repositories = _repositories.Where(x => x.Repository.OrganizationId == organizationId).OrderByDescending(x => _favorites.Contains((organizationId, userId, x.Repository.Id))).ThenBy(x => x.Repository.Owner).ThenBy(x => x.Repository.Name).ToArray();
        var items = repositories.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new RepositoryDto(x.Repository.Id, x.Repository.OrganizationId, x.Repository.Owner, x.Repository.Name, x.Repository.FullName, x.Repository.DefaultBranch, x.Repository.Visibility, x.Repository.WebUrl, x.Request.Status, x.Repository.CreatedAtUtc, IsFavorite: _favorites.Contains((organizationId, userId, x.Repository.Id)))).ToArray();
        return Task.FromResult(new RepositoryPage(items, page, pageSize, repositories.Length));
    }
    public Task SetAsync(Guid organizationId, Guid repositoryId, Guid userId, bool isFavorite, CancellationToken cancellationToken) { if (isFavorite) _favorites.Add((organizationId, userId, repositoryId)); else _favorites.Remove((organizationId, userId, repositoryId)); return Task.CompletedTask; }
    public Task RemoveAsync(Guid organizationId, Guid repositoryId, Guid actorUserId, string confirmation, DateTimeOffset removedAtUtc, CancellationToken cancellationToken)
    {
        var item = _repositories.SingleOrDefault(x => x.Repository.OrganizationId == organizationId && x.Repository.Id == repositoryId);
        if (item.Repository is null) throw new RepoNavAI.Application.Common.Exceptions.NotFoundException("Repository was not found.");
        if (!string.Equals(item.Repository.FullName, confirmation, StringComparison.OrdinalIgnoreCase)) throw new FluentValidation.ValidationException($"Enter {item.Repository.FullName} to confirm repository removal.");
        _repositories.Remove(item); _favorites.RemoveWhere(x => x.OrganizationId == organizationId && x.RepositoryId == repositoryId); return Task.CompletedTask;
    }
    public Task<IndexingRequestDto?> GetIndexingRequestAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) => Task.FromResult(_repositories.Where(x => x.Repository.OrganizationId == organizationId && x.Repository.Id == repositoryId).Select(x => new IndexingRequestDto(x.Request.Id, x.Request.RepositoryId, x.Request.Status, x.Request.Checkpoint, x.Request.AttemptCount, x.Request.CommitSha, x.Request.ErrorCode, x.Request.ErrorMessage, x.Request.CreatedAtUtc, x.Request.CompletedAtUtc)).FirstOrDefault());
    public Task<IReadOnlyCollection<RepositoryEndpointDto>> ListEndpointsAsync(Guid organizationId, Guid repositoryId, string? method, string? search, bool? requiresAuthorization, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<RepositoryEndpointDto>>([]);
    public Task<RepositoryCapabilitiesDto> GetCapabilitiesAsync(Guid organizationId, Guid repositoryId, CancellationToken cancellationToken) => Task.FromResult(new RepositoryCapabilitiesDto(false, false, false, false, false, []));
}

public sealed class TestRepositoryProvider : IRepositoryProvider
{
    public Task<ProviderRepository?> GetAsync(GitHubRepositoryAddress address, CancellationToken cancellationToken) =>
        Task.FromResult<ProviderRepository?>(new ProviderRepository("123", address.Owner, address.Name, "main", RepositoryVisibility.Private, $"https://github.com/{address.Owner}/{address.Name}"));
}

public sealed class TestEmbeddingGenerator : IEmbeddingGenerator
{
    public string Model => "test-embedding"; public int Dimensions => 3; public bool IsConfigured => true;
    public Task<IReadOnlyList<float[]>> GenerateAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new[] { 1f, 0f, 0f }).ToArray());
}

public sealed class TestVectorStore : IVectorStore
{
    public Task UpsertAsync(Guid organizationId, Guid repositoryId, Guid snapshotId, IReadOnlyCollection<(Guid ChunkId, float[] Embedding)> embeddings, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<IReadOnlyCollection<SemanticSearchResult>> SearchAsync(Guid organizationId, Guid repositoryId, float[] query, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<SemanticSearchResult>>([new(Guid.NewGuid(), "src/Auth.cs", 10, 20, "auth source", 0.9, "abc123", "https://example.test/src")]);
}

public sealed class TestRepositoryAnswerGenerator : IRepositoryAnswerGenerator
{
    public bool IsConfigured => true; public string Model => "test-chat";
    public async IAsyncEnumerable<string> StreamAsync(string question, IReadOnlyCollection<SemanticSearchResult> sources, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); yield return "Authentication is enforced [1].";
    }
}

public sealed class TestRepositoryChatSessionStore : IRepositoryChatSessionStore
{
    public Task<Guid> StartAsync(Guid organizationId, Guid repositoryId, Guid userId, string model, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid());
    public Task FinishAsync(Guid sessionId, RepositoryChatStatus status, CancellationToken cancellationToken) => Task.CompletedTask;
}
