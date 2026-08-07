namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Validation;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertyProcessingCommandValidatorTests
{
    [Fact]
    public void Activation_requires_an_operation_id_and_actor()
    {
        ActivatePropertyProcessingCommand command = new(
            Guid.NewGuid(),
            Guid.Empty,
            "GB",
            "gb-hostel",
            1,
            "eu-west-2",
            "uk-no-transfer",
            "guest-operational",
            1,
            [],
            true,
            1,
            string.Empty);

        string[] errors = new ActivatePropertyProcessingCommandValidator()
            .Validate(command)
            .ToArray();

        Assert.Contains("OperationId is required.", errors);
        Assert.Contains(
            errors,
            error => error.StartsWith("Actor id", StringComparison.Ordinal));
    }

    [Fact]
    public void Suspension_requires_an_operation_id_and_actor()
    {
        SuspendPropertyProcessingCommand command = new(
            Guid.NewGuid(),
            Guid.Empty,
            true,
            1,
            string.Empty);

        string[] errors = new SuspendPropertyProcessingCommandValidator()
            .Validate(command)
            .ToArray();

        Assert.Contains("OperationId is required.", errors);
        Assert.Contains(
            errors,
            error => error.StartsWith("Actor id", StringComparison.Ordinal));
    }

    [Fact]
    public void Confirmation_is_left_to_the_handler_for_a_stable_domain_error()
    {
        SuspendPropertyProcessingCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            false,
            1,
            "user:owner");

        Assert.Empty(
            new SuspendPropertyProcessingCommandValidator().Validate(command));
    }
}
