namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Validation;
using BunkFy.Modules.DataRights.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CreateDataRightsCaseCommandValidatorTests
{
    [Theory]
    [InlineData(
        DataRightsOperation.Restriction,
        DataRightsRestrictionDirective.Unknown,
        false)]
    [InlineData(
        DataRightsOperation.Restriction,
        DataRightsRestrictionDirective.Apply,
        true)]
    [InlineData(
        DataRightsOperation.Restriction,
        DataRightsRestrictionDirective.Release,
        true)]
    [InlineData(
        DataRightsOperation.AccessExport,
        DataRightsRestrictionDirective.Apply,
        false)]
    public void Restriction_directive_must_match_requested_operations(
        DataRightsOperation operations,
        DataRightsRestrictionDirective directive,
        bool expectedValid)
    {
        CreateDataRightsCaseCommand command = new(
            Guid.NewGuid(),
            operations,
            directive,
            DataRightsRequesterRelationship.ControllerInitiated,
            "user:operator");

        string[] errors = new CreateDataRightsCaseCommandValidator()
            .Validate(command)
            .ToArray();

        Assert.Equal(expectedValid, errors.Length == 0);
    }
}
