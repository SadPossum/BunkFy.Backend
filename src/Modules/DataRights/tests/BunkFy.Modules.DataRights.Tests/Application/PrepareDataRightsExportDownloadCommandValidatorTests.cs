namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Validation;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PrepareDataRightsExportDownloadCommandValidatorTests
{
    [Fact]
    public void Valid_coordinates_and_actor_are_accepted()
    {
        PrepareDataRightsExportDownloadCommandValidator validator = new();

        string[] errors = validator.Validate(new(
            DataRightsCaseScope.Staff,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user:privacy")).ToArray();

        Assert.Empty(errors);
    }

    [Fact]
    public void Empty_coordinates_and_oversized_actor_are_rejected()
    {
        PrepareDataRightsExportDownloadCommandValidator validator = new();

        string[] errors = validator.Validate(new(
            Scope: null!,
            CaseId: Guid.Empty,
            ArtifactId: Guid.Empty,
            ActorId: new string('a', DataRightsCase.ActorIdMaxLength + 1)))
            .ToArray();

        Assert.Equal(2, errors.Length);
    }
}
