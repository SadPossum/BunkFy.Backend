namespace BunkFy.Modules.Ingestion.Application.DataRights;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Gma.Framework.Results;

internal static class IngestionAnonymisationRestoreGraphReducer
{
    public static Result Reduce(
        IngestionAnonymisationRestoreGraph graph,
        Guid claimId,
        long selectedSourceLinkVersion,
        DateTimeOffset nowUtc)
    {
        foreach (ObservationReceipt receipt in graph.Receipts)
        {
            Result reduced = receipt.BeginAnonymisation(claimId, nowUtc);
            if (reduced.IsFailure)
            {
                return reduced;
            }
        }

        foreach (ChangeProposal proposal in graph.Proposals)
        {
            Result reduced = proposal.Anonymise(nowUtc);
            if (reduced.IsFailure)
            {
                return reduced;
            }
        }

        foreach (ReservationDispatch dispatch in graph.Dispatches)
        {
            Result reduced = dispatch.Anonymise(nowUtc);
            if (reduced.IsFailure)
            {
                return reduced;
            }
        }

        foreach (ObservationReprocessingOutput output in graph.Outputs)
        {
            Result reduced = output.Anonymise(nowUtc);
            if (reduced.IsFailure)
            {
                return reduced;
            }
        }

        return graph.SourceLink.Anonymise(
            selectedSourceLinkVersion,
            nowUtc);
    }
}
