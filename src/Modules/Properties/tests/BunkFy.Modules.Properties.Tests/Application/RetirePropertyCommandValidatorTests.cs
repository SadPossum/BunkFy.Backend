namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Validation;
using BunkFy.Modules.Properties.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetirePropertyCommandValidatorTests
{
    private readonly RetirePropertyCommandValidator validator = new();

    [Fact]
    public void Valid_actor_at_the_contract_limit_is_accepted()
    {
        RetirePropertyCommand command = new(
            Guid.NewGuid(),
            1,
            new string('a', PropertiesContractLimits.ActorIdMaxLength));

        Assert.Empty(this.validator.Validate(command));
    }

    [Theory]
    [InlineData("user:bad\0actor")]
    public void Invalid_actor_is_rejected_before_command_handling(string actorId)
    {
        RetirePropertyCommand command = new(Guid.NewGuid(), 1, actorId);

        Assert.Contains(this.validator.Validate(command), error => error.StartsWith("Actor id", StringComparison.Ordinal));
    }

    [Fact]
    public void Oversized_actor_is_rejected_before_command_handling()
    {
        RetirePropertyCommand command = new(
            Guid.NewGuid(),
            1,
            new string('a', PropertiesContractLimits.ActorIdMaxLength + 1));

        Assert.Contains(this.validator.Validate(command), error => error.StartsWith("Actor id", StringComparison.Ordinal));
    }
}
