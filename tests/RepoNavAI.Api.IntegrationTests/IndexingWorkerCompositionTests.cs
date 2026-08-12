using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RepoNavAI.Infrastructure;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class IndexingWorkerCompositionTests
{
    [Fact]
    public void ApiInfrastructure_DoesNotRegisterIndexingExecution()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(Configuration());

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(RepositoryIndexingWorker));
    }

    [Fact]
    public void WorkerComposition_RegistersExactlyOneIndexingExecutor()
    {
        var services = new ServiceCollection();

        services.AddRepositoryIndexingWorker();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(RepositoryIndexingWorker));
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
        ["Jwt:Issuer"] = "RepoNavAI",
        ["Jwt:Audience"] = "RepoNavAI.Web",
        ["Jwt:SigningKey"] = "TEST-ONLY-SIGNING-KEY-32-CHARACTERS-MINIMUM"
    }).Build();
}
