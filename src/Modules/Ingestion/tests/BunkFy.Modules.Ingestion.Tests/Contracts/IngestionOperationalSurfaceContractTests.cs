namespace BunkFy.Modules.Ingestion.Tests.Contracts;

using BunkFy.Modules.Ingestion.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionOperationalSurfaceContractTests
{
    [Fact]
    public void Connection_contracts_are_bounded()
    {
        AssertProperties<AdapterConnectionListItemDto>(
            "AdapterType",
            "ConflictPolicy",
            "ConnectionId",
            "ExecutionMode",
            "PollingIntervalSeconds",
            "Status");
        AssertProperties<AdapterConnectionListResponse>("Connections", "HasMore", "Page", "PageSize");
        AssertProperties<AdapterConnectionMutationReceiptDto>("ConnectionId", "Status", "Version");
    }

    [Fact]
    public void Activity_contracts_are_bounded()
    {
        AssertProperties<IngestionRunListItemDto>(
            "AcceptedCount",
            "CompletedAtUtc",
            "ConnectionId",
            "ErrorCode",
            "ObservedCount",
            "RejectedCount",
            "RunId",
            "StartedAtUtc",
            "Status");
        AssertProperties<IngestionRunListResponse>("HasMore", "Page", "PageSize", "Runs");

        AssertProperties<ObservationReceiptListItemDto>(
            "ConnectionId",
            "ExternalId",
            "ParserType",
            "ParserVersion",
            "ReceiptId",
            "ReceivedAtUtc",
            "SourceRecordType",
            "Status");
        AssertProperties<ObservationReceiptListResponse>("HasMore", "Page", "PageSize", "Receipts");

        AssertProperties<ObservationReprocessingAttemptListItemDto>(
            "AcceptedCount",
            "AttemptId",
            "CompletedAtUtc",
            "DuplicateCount",
            "LastErrorCode",
            "ParsedCount",
            "ParserType",
            "ParserVersion",
            "RejectedCount",
            "RequestedAtUtc",
            "StartedAtUtc",
            "Status");
        AssertProperties<ObservationReprocessingAttemptListResponse>("Attempts", "HasMore", "Page", "PageSize");
    }

    [Fact]
    public void Proposal_and_credential_contracts_do_not_leak_detail_fields()
    {
        AssertProperties<ChangeProposalListItemDto>(
            "BaseReservationDetailsRevision",
            "CreatedAtUtc",
            "ProposalId",
            "ReasonCode",
            "ReservationId",
            "Status");
        AssertProperties<ChangeProposalListResponse>("HasMore", "Page", "PageSize", "Proposals");
        AssertProperties<ChangeProposalMutationReceiptDto>(
            "ProductOperationId",
            "ProposalId",
            "Status",
            "Version");

        AssertProperties<AdapterIngressCredentialListItemDto>(
            "CredentialId",
            "ExpiresAtUtc",
            "Label",
            "LastAuthenticatedAtUtc",
            "Slot",
            "Status",
            "Version");
        AssertProperties<AdapterIngressCredentialListResponse>("Credentials", "HasMore", "Page", "PageSize");
        AssertProperties<AdapterIngressCredentialMutationReceiptDto>(
            "ConnectionId",
            "CredentialId",
            "Status",
            "Version");
    }

    private static void AssertProperties<T>(params string[] expected)
    {
        string[] actual = typeof(T)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
