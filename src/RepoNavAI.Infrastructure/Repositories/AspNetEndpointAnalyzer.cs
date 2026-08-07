using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RepoNavAI.Application.Repositories;

namespace RepoNavAI.Infrastructure.Repositories;

public sealed class AspNetEndpointAnalyzer : IRepositoryEndpointAnalyzer
{
    private static readonly Dictionary<string, string> HttpAttributes = new(StringComparer.Ordinal)
    { ["HttpGet"] = "GET", ["HttpPost"] = "POST", ["HttpPut"] = "PUT", ["HttpPatch"] = "PATCH", ["HttpDelete"] = "DELETE", ["HttpHead"] = "HEAD", ["HttpOptions"] = "OPTIONS" };

    public IReadOnlyCollection<ParsedEndpoint> Analyze(IReadOnlyCollection<RepositorySourceFile> files) =>
        files.Where(x => x.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).SelectMany(AnalyzeFile).ToArray();

    private static IEnumerable<ParsedEndpoint> AnalyzeFile(RepositorySourceFile file)
    {
        var root = CSharpSyntaxTree.ParseText(Encoding.UTF8.GetString(file.Content)).GetRoot();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var http = method.AttributeLists.SelectMany(x => x.Attributes).FirstOrDefault(x => HttpAttributes.ContainsKey(AttributeName(x)));
            if (http is null) continue;
            var controller = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            var controllerRoute = RouteAttribute(controller?.AttributeLists.SelectMany(x => x.Attributes) ?? []);
            var methodRoute = FirstStringArgument(http) ?? RouteAttribute(method.AttributeLists.SelectMany(x => x.Attributes));
            var route = Combine(controllerRoute, methodRoute).Replace("[controller]", ControllerName(controller), StringComparison.OrdinalIgnoreCase).Replace("[action]", method.Identifier.Text, StringComparison.OrdinalIgnoreCase);
            yield return Create(HttpAttributes[AttributeName(http)], route, method.Identifier.Text, file.Path, method, IsAuthorized(method, controller));
        }
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access || !access.Name.Identifier.Text.StartsWith("Map", StringComparison.Ordinal) || invocation.ArgumentList.Arguments.Count == 0) continue;
            var verb = access.Name.Identifier.Text[3..].ToUpperInvariant();
            if (verb is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")) continue;
            var route = StringValue(invocation.ArgumentList.Arguments[0].Expression); if (route is null) continue;
            var handlerExpression = invocation.ArgumentList.Arguments.Count > 1 ? invocation.ArgumentList.Arguments[1].Expression : null;
            var handler = handlerExpression switch { IdentifierNameSyntax x => x.Identifier.Text, MemberAccessExpressionSyntax x => x.ToString(), _ => $"{verb} {route}" };
            yield return Create(verb, route, handler, file.Path, invocation, HasAuthorizationConvention(invocation));
        }
    }

    private static ParsedEndpoint Create(string method, string route, string handler, string path, Microsoft.CodeAnalysis.SyntaxNode node, bool auth)
    {
        var calls = node.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(x => x.Expression.ToString()).Where(x => !x.StartsWith("Results.") && !x.StartsWith("TypedResults.")).Distinct().Take(25).ToArray();
        return new ParsedEndpoint(method, string.IsNullOrWhiteSpace(route) ? "/" : route, handler, path, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, auth, calls);
    }
    private static bool IsAuthorized(MethodDeclarationSyntax method, ClassDeclarationSyntax? controller) => HasAttribute(method.AttributeLists, "Authorize") || (controller is not null && HasAttribute(controller.AttributeLists, "Authorize")) && !HasAttribute(method.AttributeLists, "AllowAnonymous");
    private static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string name) => lists.SelectMany(x => x.Attributes).Any(x => AttributeName(x) == name);
    private static string AttributeName(AttributeSyntax value) => value.Name.ToString().Split('.').Last().Replace("Attribute", "", StringComparison.Ordinal);
    private static string? RouteAttribute(IEnumerable<AttributeSyntax> attributes) => FirstStringArgument(attributes.FirstOrDefault(x => AttributeName(x) == "Route"));
    private static string? FirstStringArgument(AttributeSyntax? attribute) => attribute?.ArgumentList?.Arguments.FirstOrDefault() is { Expression: var expression } ? StringValue(expression) : null;
    private static string? StringValue(ExpressionSyntax expression) => expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) ? literal.Token.ValueText : null;
    private static string ControllerName(ClassDeclarationSyntax? value) => value?.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal) == true ? value.Identifier.Text[..^10] : value?.Identifier.Text ?? string.Empty;
    private static string Combine(string? left, string? right) => "/" + string.Join('/', new[] { left, right }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim('/')));
    private static bool HasAuthorizationConvention(InvocationExpressionSyntax invocation) => invocation.Parent is MemberAccessExpressionSyntax { Name.Identifier.Text: "RequireAuthorization" } || invocation.Parent?.Parent?.ToString().Contains("RequireAuthorization", StringComparison.Ordinal) == true;
}
