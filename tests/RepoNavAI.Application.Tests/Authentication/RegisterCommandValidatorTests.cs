using FluentAssertions;
using RepoNavAI.Application.Authentication;
using Xunit;

namespace RepoNavAI.Application.Tests.Authentication;

public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithProductionStrengthInput_Succeeds()
    {
        var result = await _validator.ValidateAsync(new RegisterCommand("dev@example.com", "StrongPassword!9", "Ada Lovelace"));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase123!")]
    [InlineData("ALLUPPERCASE123!")]
    [InlineData("NoNumbersHere!")]
    public async Task Validate_WithWeakPassword_Fails(string password)
    {
        var result = await _validator.ValidateAsync(new RegisterCommand("dev@example.com", password, "Ada"));
        result.IsValid.Should().BeFalse();
    }
}
