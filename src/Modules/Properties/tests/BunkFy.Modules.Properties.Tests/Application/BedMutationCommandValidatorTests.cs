namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Validation;
using Xunit;

[Trait("Category", "Unit")]
public sealed class BedMutationCommandValidatorTests
{
    [Fact]
    public void Single_add_and_update_require_operation_ids()
    {
        AddBedCommandValidator addValidator = new();
        UpdateBedCommandValidator updateValidator = new();

        Assert.Contains(
            "OperationId is required.",
            addValidator.Validate(new AddBedCommand(
                Guid.Empty,
                Guid.NewGuid(),
                Guid.NewGuid(),
                ExpectedRoomVersion: 1,
                "A")));
        Assert.Contains(
            "OperationId is required.",
            updateValidator.Validate(new UpdateBedCommand(
                Guid.Empty,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                ExpectedRoomVersion: 1,
                "A")));
    }

    [Fact]
    public void Batch_validator_covers_identity_cardinality_and_normalized_labels()
    {
        AddBedsCommandValidator validator = new();
        AddBedsCommand command = new(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            ExpectedRoomVersion: 0,
            [" A ", "A"]);

        string[] errors = [.. validator.Validate(command)];

        Assert.Contains("OperationId is required.", errors);
        Assert.Contains("Property id is required.", errors);
        Assert.Contains("Room id is required.", errors);
        Assert.Contains("Expected room version must be positive.", errors);
        Assert.Contains("Bed labels must be unique.", errors);
    }
}
