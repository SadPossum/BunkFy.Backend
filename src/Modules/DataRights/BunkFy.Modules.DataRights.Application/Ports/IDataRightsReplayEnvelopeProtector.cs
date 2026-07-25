namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public interface IDataRightsReplayEnvelopeProtector
{
    Result<DataRightsProtectedReplayEnvelope> Protect(
        DataRightsProcessingLedgerSnapshot ledger,
        Guid recordId);

    Result<Guid> Unprotect(
        DataRightsProcessingLedgerSnapshot ledger,
        DataRightsProtectedReplayEnvelope envelope);
}
