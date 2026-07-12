namespace BunkFy.Modules.Ingestion.Admin.Contracts;

public static class IngestionAdminOperationNames
{
    public const string AdapterTypeList = "ingestion.adapter-types.list";
    public const string ParserTypeList = "ingestion.parser-types.list";
    public const string ConnectionList = "ingestion.connections.list";
    public const string ConnectionGet = "ingestion.connections.get";
    public const string ConnectionHealth = "ingestion.connections.health";
    public const string ConnectionCreate = "ingestion.connections.create";
    public const string ConnectionUpdate = "ingestion.connections.update";
    public const string ConnectionPollingScheduleConfigure = "ingestion.connections.polling-schedule.configure";
    public const string ConnectionPollingScheduleClear = "ingestion.connections.polling-schedule.clear";
    public const string ConnectionEnable = "ingestion.connections.enable";
    public const string ConnectionDisable = "ingestion.connections.disable";
    public const string ConnectionResetCheckpoint = "ingestion.connections.reset-checkpoint";
    public const string CredentialList = "ingestion.credentials.list";
    public const string CredentialCreate = "ingestion.credentials.create";
    public const string CredentialRevoke = "ingestion.credentials.revoke";
    public const string RunList = "ingestion.runs.list";
    public const string RunGet = "ingestion.runs.get";
    public const string RunEnqueue = "ingestion.runs.enqueue";
    public const string RunRetry = "ingestion.runs.retry";
    public const string RunCancel = "ingestion.runs.cancel";
    public const string ReceiptList = "ingestion.receipts.list";
    public const string ReceiptGet = "ingestion.receipts.get";
    public const string ReceiptRawPayloadDownload = "ingestion.receipts.raw-payload.download";
    public const string ReprocessingList = "ingestion.reprocessing.list";
    public const string ReprocessingGet = "ingestion.reprocessing.get";
    public const string ReprocessingEnqueue = "ingestion.reprocessing.enqueue";
    public const string ReprocessingCancel = "ingestion.reprocessing.cancel";
    public const string RetentionRawPayloadPurge = "ingestion.retention.raw-payloads.purge";
    public const string RetentionSensitiveHistoryRedact = "ingestion.retention.sensitive-history.redact";
    public const string LegalHoldList = "ingestion.legal-holds.list";
    public const string LegalHoldGet = "ingestion.legal-holds.get";
    public const string LegalHoldPlace = "ingestion.legal-holds.place";
    public const string LegalHoldRelease = "ingestion.legal-holds.release";
    public const string ProposalList = "ingestion.proposals.list";
    public const string ProposalGet = "ingestion.proposals.get";
    public const string ProposalAccept = "ingestion.proposals.accept";
    public const string ProposalReject = "ingestion.proposals.reject";
}
