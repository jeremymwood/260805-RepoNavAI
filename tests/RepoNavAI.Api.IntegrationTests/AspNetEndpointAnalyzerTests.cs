using System.Text;
using FluentAssertions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class AspNetEndpointAnalyzerTests
{
    private readonly AspNetEndpointAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_ExtractsControllerRouteAuthorizationAndCalls()
    {
        const string source = """
            [Authorize]
            [Route("api/[controller]")]
            public class OrdersController
            {
                [HttpGet("{id}")]
                public object Get(Guid id) => sender.Send(new GetOrder(id));
            }
            """;

        var endpoint = _analyzer.Analyze([File("Controllers/OrdersController.cs", source)]).Should().ContainSingle().Subject;
        endpoint.HttpMethod.Should().Be("GET");
        endpoint.Route.Should().Be("/api/Orders/{id}");
        endpoint.Handler.Should().Be("Get");
        endpoint.RequiresAuthorization.Should().BeTrue();
        endpoint.DownstreamSymbols.Should().Contain("sender.Send");
    }

    [Fact]
    public void Analyze_ExtractsMinimalApiRoute()
    {
        const string source = "app.MapPost(\"/orders\", CreateOrder).RequireAuthorization();";

        var endpoint = _analyzer.Analyze([File("Program.cs", source)]).Should().ContainSingle().Subject;
        endpoint.HttpMethod.Should().Be("POST");
        endpoint.Route.Should().Be("/orders");
        endpoint.Handler.Should().Be("CreateOrder");
        endpoint.RequiresAuthorization.Should().BeTrue();
    }

    [Fact]
    public void Analyze_IgnoresUnsupportedDynamicRoutesRatherThanInventingOne()
    {
        _analyzer.Analyze([File("Program.cs", "app.MapGet(routeFromConfiguration, Handler);")]).Should().BeEmpty();
    }

    private static RepositorySourceFile File(string path, string content) => new(path, "csharp", Encoding.UTF8.GetBytes(content));
}
