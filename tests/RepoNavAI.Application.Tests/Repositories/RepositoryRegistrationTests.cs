using FluentAssertions;
using RepoNavAI.Application.Common.Identity;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Domain.Repositories;
using Xunit;

namespace RepoNavAI.Application.Tests.Repositories;

public sealed class RepositoryRegistrationTests
{
    [Theory]
    [InlineData("https://github.com/OpenAI/openai-dotnet", "openai", "openai-dotnet")]
    [InlineData("https://github.com/OpenAI/openai-dotnet.git/", "openai", "openai-dotnet")]
    public void TryParse_NormalizesSupportedGitHubUrl(string url, string owner, string name)
    {
        GitHubRepositoryAddress.TryParse(url, out var result).Should().BeTrue();
        result.Should().Be(new GitHubRepositoryAddress(owner, name));
    }

    [Theory]
    [InlineData("http://github.com/openai/openai-dotnet")]
    [InlineData("https://gitlab.com/openai/openai-dotnet")]
    [InlineData("https://github.com/openai")]
    [InlineData("https://github.com/openai/openai-dotnet/issues")]
    [InlineData("git@github.com:openai/openai-dotnet.git")]
    public void TryParse_RejectsUnsupportedRepositoryAddress(string url) =>
        GitHubRepositoryAddress.TryParse(url, out _).Should().BeFalse();

    [Fact]
    public async Task Register_VerifiesAccessAndCreatesPendingIndexingRequest()
    {
        var userId = Guid.NewGuid();
        var organization = new Organization("Acme", "acme");
        organization.AddMember(userId, OrganizationRole.Member);
        var store = new RegistrationStore();
        var provider = new ProviderStub(new ProviderRepository("42", "openai", "openai-dotnet", "main", RepositoryVisibility.Public, "https://github.com/openai/openai-dotnet"));
        var handler = new RegisterRepositoryHandler(new OrganizationAccess(new OrganizationStore(organization)), provider, store, new CurrentUserStub(userId));

        var result = await handler.Handle(new RegisterRepositoryCommand(organization.Id, "https://github.com/OpenAI/openai-dotnet.git"), CancellationToken.None);

        result.FullName.Should().Be("openai/openai-dotnet");
        result.IndexingStatus.Should().Be(IndexingRequestStatus.Pending);
        store.Repository.Should().NotBeNull();
        store.Repository!.RegisteredByUserId.Should().Be(userId);
        store.IndexingRequest.Should().NotBeNull();
        store.IndexingRequest!.RepositoryId.Should().Be(store.Repository.Id);
        store.Saved.Should().BeTrue();
    }

    private sealed class CurrentUserStub(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => userId;
        public string Email => "member@example.com";
    }

    private sealed class ProviderStub(ProviderRepository repository) : IRepositoryProvider
    {
        public Task<ProviderRepository?> GetAsync(GitHubRepositoryAddress address, CancellationToken cancellationToken) => Task.FromResult<ProviderRepository?>(repository);
    }

    private sealed class RegistrationStore : IRepositoryRegistrationRepository
    {
        public RegisteredRepository? Repository { get; private set; }
        public RepositoryIndexingRequest? IndexingRequest { get; private set; }
        public bool Saved { get; private set; }
        public Task<bool> ExistsAsync(Guid organizationId, string owner, string name, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(RegisteredRepository repository, RepositoryIndexingRequest indexingRequest, CancellationToken cancellationToken) { Repository = repository; IndexingRequest = indexingRequest; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) { Saved = true; return Task.CompletedTask; }
    }

    private sealed class OrganizationStore(Organization organization) : IOrganizationRepository
    {
        public Task<Organization?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == organization.Id ? organization : null);
        public Task AddAsync(Organization value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<OrganizationInvitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<OrganizationInvitation?> GetInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<bool> HasPendingInvitationAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> IsEmailMemberAsync(Guid organizationId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
