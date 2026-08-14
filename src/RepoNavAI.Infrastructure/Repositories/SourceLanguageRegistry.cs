using System.Text;

namespace RepoNavAI.Infrastructure.Repositories;

public sealed record SourceLanguage(string Name, bool IsExecutable);
public sealed record SourceFileClassification(SourceLanguage? Language, string? SkipReason)
{
    public bool IsSupported => Language is not null && SkipReason is null;
}

public sealed class SourceLanguageRegistry
{
    public const string Unsupported = "unsupported_extension";
    public const string Generated = "generated";
    public const string Vendored = "vendored";
    public const string Binary = "binary_content";

    private static readonly IReadOnlyDictionary<string, SourceLanguage> Languages = new Dictionary<string, SourceLanguage>(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = new("csharp", true), [".csproj"] = new("msbuild", false), [".sln"] = new("solution", false),
        [".ts"] = new("typescript", true), [".tsx"] = new("typescript", true), [".js"] = new("javascript", true), [".jsx"] = new("javascript", true),
        [".py"] = new("python", true), [".pyi"] = new("python", true),
        [".c"] = new("c", true), [".h"] = new("c", true), [".cc"] = new("cpp", true), [".cpp"] = new("cpp", true), [".cxx"] = new("cpp", true), [".hpp"] = new("cpp", true), [".hh"] = new("cpp", true),
        [".go"] = new("go", true), [".java"] = new("java", true), [".rs"] = new("rust", true), [".rb"] = new("ruby", true), [".kt"] = new("kotlin", true), [".kts"] = new("kotlin", true),
        [".json"] = new("json", false), [".md"] = new("markdown", false), [".yml"] = new("yaml", false), [".yaml"] = new("yaml", false), [".toml"] = new("toml", false), [".xml"] = new("xml", false),
        [".sh"] = new("shell", true), [".ps1"] = new("powershell", true), [".sql"] = new("sql", true),
    };
    private static readonly HashSet<string> VendorDirectories = new(StringComparer.OrdinalIgnoreCase) { ".git", "node_modules", "vendor", "vendors", "third_party", "third-party", "external", "deps" };
    private static readonly HashSet<string> GeneratedDirectories = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "dist", "build", "coverage", "target", ".next", ".cache", "generated" };
    private static readonly string[] GeneratedSuffixes = [".g.cs", ".designer.cs", ".generated.cs", ".min.js", ".min.css"];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public SourceFileClassification ClassifyPath(string path)
    {
        var extension = Path.GetExtension(path);
        if (!Languages.TryGetValue(extension, out var language)) return new(null, Unsupported);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(VendorDirectories.Contains)) return new(language, Vendored);
        if (segments.Any(GeneratedDirectories.Contains) || GeneratedSuffixes.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))) return new(language, Generated);
        return new(language, null);
    }

    public static bool IsText(ReadOnlySpan<byte> content)
    {
        if (content.Contains((byte)0)) return false;
        try { StrictUtf8.GetCharCount(content); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    public static bool IsExecutableLanguage(string language) => Languages.Values.Any(value => value.IsExecutable && value.Name.Equals(language, StringComparison.OrdinalIgnoreCase));
}
