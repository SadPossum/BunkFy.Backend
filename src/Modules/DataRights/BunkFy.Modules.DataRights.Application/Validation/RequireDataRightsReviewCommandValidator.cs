namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RequireDataRightsReviewCommandValidator
    : ICommandValidator<RequireDataRightsReviewCommand>
{
    public IEnumerable<string> Validate(RequireDataRightsReviewCommand command) =>
        DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId);
}
