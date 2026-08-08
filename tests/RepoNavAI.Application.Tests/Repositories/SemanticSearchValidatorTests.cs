using FluentAssertions;
using RepoNavAI.Application.Repositories;
using Xunit;

namespace RepoNavAI.Application.Tests.Repositories;

public sealed class SemanticSearchValidatorTests
{
    private readonly SemanticSearchValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmptyQueries(string query) => _validator.Validate(new SemanticSearchQuery(Guid.NewGuid(), Guid.NewGuid(), query)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public void Validate_RejectsUnsafeLimits(int limit) => _validator.Validate(new SemanticSearchQuery(Guid.NewGuid(), Guid.NewGuid(), "authorization", limit)).IsValid.Should().BeFalse();
}
