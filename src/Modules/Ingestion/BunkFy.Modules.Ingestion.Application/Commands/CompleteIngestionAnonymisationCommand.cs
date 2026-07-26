namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

internal sealed record CompleteIngestionAnonymisationCommand(
    DataRightsAnonymisationContributionRequest Request)
    : ITransactionalCommand<
        IngestionAnonymisationExecutionProof>;
