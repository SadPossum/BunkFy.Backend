namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Validation;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RoomMutationCommandValidatorTests
{
    [Fact]
    public void Room_creation_requires_an_operation_id()
    {
        CreateRoomCommandValidator validator = new();
        CreateRoomCommand command = new(
            Guid.Empty,
            Guid.NewGuid(),
            ExpectedPropertyVersion: 1,
            "101",
            null,
            null);

        Assert.Contains("OperationId is required.", validator.Validate(command));
    }

    [Fact]
    public void Room_update_requires_an_operation_id()
    {
        UpdateRoomCommandValidator validator = new();
        UpdateRoomCommand command = new(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExpectedVersion: 1,
            "101",
            null,
            null);

        Assert.Contains("OperationId is required.", validator.Validate(command));
    }
}
