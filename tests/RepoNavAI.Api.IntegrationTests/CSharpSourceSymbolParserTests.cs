using FluentAssertions;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class CSharpSourceSymbolParserTests
{
    [Fact]
    public void Parse_ExtractsQualifiedDeclarationsAndSourceLines()
    {
        const string source = "namespace Acme;\npublic class Service\n{\n    public string Name { get; init; } = \"\";\n    public void Run() { }\n}";
        var symbols = new CSharpSourceSymbolParser().Parse("Service.cs", System.Text.Encoding.UTF8.GetBytes(source));
        symbols.Should().Contain(x => x.QualifiedName == "Acme.Service" && x.Kind == SymbolKind.Class && x.Line == 2);
        symbols.Should().Contain(x => x.QualifiedName == "Acme.Service.Run" && x.Kind == SymbolKind.Method && x.Line == 5);
    }
}
