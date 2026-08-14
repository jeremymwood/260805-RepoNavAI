using FluentAssertions;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class SourceLanguageRegistryTests
{
    private readonly SourceLanguageRegistry registry = new();

    [Theory]
    [InlineData("src/app.py", "python")]
    [InlineData("src/main.c", "c")]
    [InlineData("src/main.cpp", "cpp")]
    [InlineData("cmd/main.go", "go")]
    [InlineData("src/Main.java", "java")]
    [InlineData("src/lib.rs", "rust")]
    [InlineData("lib/app.rb", "ruby")]
    [InlineData("src/Main.kt", "kotlin")]
    public void ClassifyPath_UsesExtensibleLanguageMappings(string path, string expected)
    {
        var result = registry.ClassifyPath(path);
        result.IsSupported.Should().BeTrue();
        result.Language!.Name.Should().Be(expected);
        result.Language.IsExecutable.Should().BeTrue();
    }

    [Theory]
    [InlineData("vendor/app.py", SourceLanguageRegistry.Vendored)]
    [InlineData("generated/client.go", SourceLanguageRegistry.Generated)]
    [InlineData("src/file.swift", SourceLanguageRegistry.Unsupported)]
    public void ClassifyPath_ReturnsNonSensitiveSkipReasons(string path, string reason) => registry.ClassifyPath(path).SkipReason.Should().Be(reason);

    [Fact]
    public void IsText_RejectsNullAndInvalidUtf8Bytes()
    {
        SourceLanguageRegistry.IsText("safe text"u8).Should().BeTrue();
        SourceLanguageRegistry.IsText([0x00, 0x01]).Should().BeFalse();
        SourceLanguageRegistry.IsText([0xff, 0xfe]).Should().BeFalse();
    }
}
