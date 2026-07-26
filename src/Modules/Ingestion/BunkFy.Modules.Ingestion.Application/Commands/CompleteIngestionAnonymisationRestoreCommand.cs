namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

internal sealed record CompleteIngestionAnonymisationRestoreCommand(
    DataRightsAnonymisationRestoreRequest Request)
    : ITransactionalCommand<DataRightsAnonymisationRestoreProof>;
