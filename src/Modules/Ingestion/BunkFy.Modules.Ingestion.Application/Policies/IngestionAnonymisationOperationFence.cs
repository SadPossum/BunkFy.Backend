namespace BunkFy.Modules.Ingestion.Application.Policies;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Properties.Contracts;

internal static class IngestionAnonymisationOperationFence
{
    public static string ComputeSha256(
        IngestionAnonymisationEligibilitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        StringBuilder canonical = new();
        Append(canonical, snapshot.SourceLink.Id);
        Append(canonical, snapshot.SourceLink.PropertyId);
        Append(canonical, snapshot.SourceLink.ConnectionId);
        Append(canonical, snapshot.SourceLink.ReservationId);
        Append(canonical, (int)snapshot.SourceLink.State);
        Append(canonical, snapshot.SourceLink.LastObservedReceiptId);
        Append(canonical, snapshot.SourceLink.LastAppliedReceiptId);
        Append(canonical, snapshot.SourceLink.ActiveProductOperationId);
        Append(canonical, snapshot.SourceLink.DeferredReceiptId);
        Append(canonical, snapshot.SourceLink.Version);
        Append(canonical, (int)snapshot.Connection.State);
        Append(canonical, snapshot.Connection.Version);
        Append(canonical, snapshot.ActiveLegalHoldCount);

        IngestionAnonymisationPropertySnapshot? property = snapshot.Property;
        Append(canonical, property is not null);
        if (property is not null)
        {
            Append(canonical, property.IsKnown);
            Append(canonical, property.IsActive);
            Append(canonical, (int)property.ProcessingStatus);
            Append(canonical, property.TopologySourceVersion);
            Append(canonical, property.PolicySourceVersion);
            Append(canonical, property.RetentionFenceVersion);
            AppendPolicy(canonical, property.GovernancePolicy);
        }

        foreach (IngestionAnonymisationReceiptSnapshot receipt in
                 snapshot.Receipts.OrderBy(item => item.Id))
        {
            Append(canonical, "receipt");
            Append(canonical, receipt.Id);
            Append(canonical, (int)receipt.State);
            Append(canonical, (int)receipt.RawPayloadRetentionState);
            Append(canonical, receipt.RawPayloadVersion);
            Append(canonical, receipt.ActiveReprocessingAttemptId);
            Append(canonical, receipt.ReprocessingReservationExpiresAtUtc);
            Append(canonical, receipt.SourceReceiptId);
            Append(canonical, receipt.ReprocessingAttemptId);
        }

        foreach (IngestionAnonymisationProposalSnapshot proposal in
                 snapshot.Proposals.OrderBy(item => item.Id))
        {
            Append(canonical, "proposal");
            Append(canonical, proposal.Id);
            Append(canonical, proposal.ReceiptId);
            Append(canonical, (int)proposal.State);
            Append(canonical, proposal.Version);
        }

        foreach (IngestionAnonymisationDispatchSnapshot dispatch in
                 snapshot.Dispatches.OrderBy(item => item.Id))
        {
            Append(canonical, "dispatch");
            Append(canonical, dispatch.Id);
            Append(canonical, dispatch.ReceiptId);
            Append(canonical, (int)dispatch.State);
            Append(canonical, dispatch.Version);
        }

        foreach (IngestionAnonymisationAttemptSnapshot attempt in
                 snapshot.Attempts.OrderBy(item => item.Id))
        {
            Append(canonical, "attempt");
            Append(canonical, attempt.Id);
            Append(canonical, attempt.SourceReceiptId);
            Append(canonical, (int)attempt.State);
            Append(canonical, attempt.Version);
        }

        foreach (IngestionAnonymisationOutputSnapshot output in
                 snapshot.Outputs
                     .OrderBy(item => item.AttemptId)
                     .ThenBy(item => item.OutputIndex)
                     .ThenBy(item => item.Id))
        {
            Append(canonical, "output");
            Append(canonical, output.Id);
            Append(canonical, output.AttemptId);
            Append(canonical, output.OutputIndex);
            Append(canonical, (int)output.Disposition);
            Append(canonical, output.ReceiptId);
        }

        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendPolicy(
        StringBuilder canonical,
        PropertyGovernancePolicyBinding? policy)
    {
        Append(canonical, policy is not null);
        if (policy is null)
        {
            return;
        }

        Append(canonical, policy.OperatingCountryCode);
        Append(canonical, policy.PolicyId);
        Append(canonical, policy.PolicyVersion);
        Append(canonical, policy.DataRegionId);
        Append(canonical, policy.TransferProfileId);
        Append(canonical, policy.RetentionPolicyId);
        Append(canonical, policy.RetentionPolicyVersion);
        Append(canonical, policy.ContentSha256);
        Append(canonical, policy.PolicyEffectiveAtUtc);
        Append(canonical, policy.PolicyExpiresAtUtc);
        Append(canonical, policy.ActivatedAtUtc);
        foreach (PropertyGovernanceAcknowledgement acknowledgement in
                 policy.Acknowledgements
                     .OrderBy(item => item.AcknowledgementId, StringComparer.Ordinal)
                     .ThenBy(item => item.AcknowledgementVersion))
        {
            Append(canonical, acknowledgement.AcknowledgementId);
            Append(canonical, acknowledgement.AcknowledgementVersion);
        }
    }

    private static void Append(StringBuilder target, object? value)
    {
        string canonical = value switch
        {
            null => "<null>",
            DateTimeOffset timestamp => timestamp.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(
                format: null,
                CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        target.Append(canonical.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(canonical);
    }
}
