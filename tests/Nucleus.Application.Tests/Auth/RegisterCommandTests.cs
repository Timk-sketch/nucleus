using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Nucleus.Application.Auth.Commands;
using Xunit;

namespace Nucleus.Application.Tests.Auth;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Theory]
    [InlineData("", "test@example.com", "Password1", "First", "Last")]
    [InlineData("Tenant", "not-an-email", "Password1", "First", "Last")]
    [InlineData("Tenant", "test@example.com", "short", "First", "Last")]
    [InlineData("Tenant", "test@example.com", "Password1", "", "Last")]
    public void Validate_InvalidInput_HasErrors(string tenantName, string email, string password, string firstName, string lastName)
    {
        var command = new RegisterCommand(tenantName, email, password, firstName, lastName);
        var result = _validator.TestValidate(command);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Validate_ValidInput_NoErrors()
    {
        var command = new RegisterCommand("TK Digital", "tim@tkdigital.com", "SecurePass1!", "Tim", "K");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
