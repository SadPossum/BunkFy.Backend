namespace BunkFy.Modules.Ingestion.Application;

using BunkFy.DataGovernance;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Contracts.Adapters;
using BunkFy.Modules.Ingestion.Domain.Errors;

public static class IngestionApplicationErrors
{
    public static readonly Error ScopeRequired = new("Ingestion.ScopeRequired", "An active scope is required.");
    public static readonly Error ConnectionNotFound = new("Ingestion.ConnectionNotFound", "The adapter connection was not found.");
    public static readonly Error PropertyNotFound = new("Ingestion.PropertyNotFound", "The property was not found in Ingestion's local projection.");
    public static Error CountryPolicyDenied(CountryPolicyDecisionReason reason) => new(
        $"Ingestion.CountryPolicyDenied.{reason}",
        "The property's current country policy does not permit this operation.");
    public static IReadOnlyList<Error> CountryPolicyDenials { get; } =
        Enum.GetValues<CountryPolicyDecisionReason>()
            .Where(reason => reason is not CountryPolicyDecisionReason.Unknown and not CountryPolicyDecisionReason.Allowed)
            .Select(CountryPolicyDenied)
            .ToArray();
    public static readonly Error ConnectionStatusInvalid = new("Ingestion.ConnectionStatusInvalid", "The adapter connection status filter is invalid.");
    public static readonly Error RunNotFound = new("Ingestion.RunNotFound", "The ingestion run was not found.");
    public static readonly Error RunStatusInvalid = new("Ingestion.RunStatusInvalid", "The ingestion run status filter is invalid.");
    public static readonly Error RunNotTaskManaged = new("Ingestion.RunNotTaskManaged", "The ingestion run is remotely leased and is not controlled by TaskRuntime.");
    public static readonly Error RunConnectionMismatch = new("Ingestion.RunConnectionMismatch", "The ingestion run does not belong to the adapter connection.");
    public static readonly Error PayloadHashMismatch = new("Ingestion.PayloadHashMismatch", "The observation payload does not match its declared hash.");
    public static readonly Error ObservationInvalid = new("Ingestion.ObservationInvalid", "The observation envelope is invalid.");
    public static readonly Error OperationIdentityConflict = new("Ingestion.OperationIdentityConflict", "The adapter operation id was reused for different observation content.");
    public static readonly Error SourceRevisionConflict = new("Ingestion.SourceRevisionConflict", "The source revision was reused for different observation content.");
    public static readonly Error AdapterRunnerNotRegistered = new("Ingestion.AdapterRunnerNotRegistered", "No local runner is registered for the adapter type.");
    public static readonly Error AdapterTypeNotRegistered = new("Ingestion.AdapterTypeNotRegistered", "The adapter type is not registered in this host composition.");
    public static readonly Error AdapterExecutionModeUnsupported = new("Ingestion.AdapterExecutionModeUnsupported", "The adapter type does not support the connection execution mode.");
    public static readonly Error AdapterExecutionModeNotTaskRunnable = new("Ingestion.AdapterExecutionModeNotTaskRunnable", "Push adapter connections receive observations directly and cannot be enqueued as adapter tasks.");
    public static readonly Error AdapterDescriptorMismatch = new("Ingestion.AdapterDescriptorMismatch", "The local runner descriptor does not match the registered adapter capability.");
    public static readonly Error PollingIntervalBelowAdapterMinimum = new("Ingestion.PollingIntervalBelowAdapterMinimum", "The polling interval is below the adapter provider's declared minimum.");
    public static readonly Error ConnectionRunAlreadyActive = new("Ingestion.ConnectionRunAlreadyActive", "The adapter connection already has an active source run.");
    public static readonly Error IngressCredentialNotFound = new("Ingestion.IngressCredentialNotFound", "The adapter ingress credential was not found.");
    public static readonly Error IngressCredentialLimitReached = new("Ingestion.IngressCredentialLimitReached", "The adapter connection already has the maximum number of active ingress credentials.");
    public static readonly Error IngressCredentialUnauthorized = new("Ingestion.IngressCredentialUnauthorized", "The adapter ingress credential is invalid.");
    public static readonly Error IngressCredentialsRequirePushMode = new("Ingestion.IngressCredentialsRequirePushMode", "Adapter ingress credentials require push or remote polling execution mode.");
    public static readonly Error IngressSubmissionInvalid = new("Ingestion.IngressSubmissionInvalid", "The adapter ingress submission exceeds the allowed record or payload limits.");
    public static readonly Error RemoteLeaseClaimInvalid = new("Ingestion.RemoteLeaseClaimInvalid", "The remote adapter lease claim is invalid.");
    public static readonly Error RemoteLeaseDescriptorMismatch = new("Ingestion.RemoteLeaseDescriptorMismatch", "The remote adapter descriptor does not match the registered connection capability.");
    public static readonly Error RemoteLeaseUnavailable = new("Ingestion.RemoteLeaseUnavailable", "The remote adapter connection is currently assigned.");
    public static readonly Error TaskContextMismatch = new("Ingestion.TaskContextMismatch", "The task execution context does not match the ingestion run.");
    public static readonly Error AdapterCompletionMismatch = new("Ingestion.AdapterCompletionMismatch", "The adapter completion does not match its assigned run.");
    public static Error AdapterConfigurationReferenceInvalid => AdapterConfigurationMaterialErrors.ReferenceInvalid;
    public static Error AdapterConfigurationMaterialNotFound => AdapterConfigurationMaterialErrors.MaterialNotFound;
    public static Error AdapterConfigurationSchemaMismatch => AdapterConfigurationMaterialErrors.SchemaMismatch;
    public static Error AdapterConfigurationMaterialInvalid => AdapterConfigurationMaterialErrors.MaterialInvalid;
    public static readonly Error ReceiptNotFound = new("Ingestion.ReceiptNotFound", "The observation receipt was not found.");
    public static readonly Error ReceiptStatusInvalid = new("Ingestion.ReceiptStatusInvalid", "The observation receipt status filter is invalid.");
    public static readonly Error OperatorValueInvalid = new("Ingestion.OperatorValueInvalid", "An operator option value is invalid.");
    public static readonly Error SecretReferenceUpdateInvalid = new("Ingestion.SecretReferenceUpdateInvalid", "The secret reference update must explicitly keep, replace, or clear the current reference.");
    public static readonly Error ReceiptNotPending = new("Ingestion.ReceiptNotPending", "The observation receipt is already terminal.");
    public static readonly Error ReservationSourceNotLinked = new("Ingestion.ReservationSourceNotLinked", "The external reservation source is not linked to a reservation.");
    public static readonly Error NormalizedReservationObservationInvalid = new("Ingestion.NormalizedReservationObservationInvalid", "The normalized reservation observation is invalid.");
    public static readonly Error ReservationBaselineUnavailable = new("Ingestion.ReservationBaselineUnavailable", "The linked reservation has no accepted adapter baseline for a safe change proposal.");
    public static readonly Error RawPayloadInvalid = new("Ingestion.RawPayloadInvalid", "The raw observation payload is unavailable or invalid.");
    public static readonly Error RawPayloadPurgeInProgress = new("Ingestion.RawPayloadPurgeInProgress", "The raw observation payload is being purged.");
    public static readonly Error RawPayloadUnavailable = new("Ingestion.RawPayloadUnavailable", "The raw observation payload has been purged.");
    public static readonly Error RetentionTaskOptionsInvalid = new("Ingestion.RetentionTaskOptionsInvalid", "The retention task options are invalid.");
    public static readonly Error ProposalNotFound = new("Ingestion.ProposalNotFound", "The change proposal was not found.");
    public static readonly Error ProposalStatusInvalid = new("Ingestion.ProposalStatusInvalid", "The change proposal status filter is invalid.");
    public static readonly Error ProposalDecisionConflict = new("Ingestion.ProposalDecisionConflict", "The change proposal was already decided differently.");
    public static readonly Error LegalHoldNotFound = new("Ingestion.LegalHoldNotFound", "The legal hold was not found.");
    public static readonly Error LegalHoldStatusInvalid = new("Ingestion.LegalHoldStatusInvalid", "The legal hold status filter is invalid.");
    public static readonly Error LegalHoldPurgeInProgress = new("Ingestion.LegalHoldPurgeInProgress", "A raw payload deletion is already in progress for the property; retry hold placement after it finishes.");
    public static readonly Error ReprocessingAttemptNotFound = new("Ingestion.ReprocessingAttemptNotFound", "The observation reprocessing attempt was not found.");
    public static readonly Error ReprocessingAttemptStatusInvalid = new("Ingestion.ReprocessingAttemptStatusInvalid", "The observation reprocessing attempt status is invalid.");
    public static readonly Error ReprocessingParserNotRegistered = new("Ingestion.ReprocessingParserNotRegistered", "The requested observation parser is not registered in this host composition.");
    public static readonly Error ReprocessingParserMismatch = new("Ingestion.ReprocessingParserMismatch", "The executable parser does not match its registered descriptor.");
    public static readonly Error ReprocessingParserSourceUnsupported = new("Ingestion.ReprocessingParserSourceUnsupported", "The parser does not support this adapter and source record type.");
    public static readonly Error ReprocessingSourceNotRejected = new("Ingestion.ReprocessingSourceNotRejected", "Only a rejected source receipt can be reprocessed.");
    public static readonly Error ReprocessingScheduleInvalid = new("Ingestion.ReprocessingScheduleInvalid", "The reprocessing schedule is outside the supported window.");
    public static readonly Error ReprocessingOutputInvalid = new("Ingestion.ReprocessingOutputInvalid", "The parser returned an invalid or undeclared observation output.");
    public static readonly Error ReprocessingOutputConflict = new("Ingestion.ReprocessingOutputConflict", "The parser returned duplicate output identities.");
    public static readonly Error ReprocessingRawPayloadInvalid = new("Ingestion.ReprocessingRawPayloadInvalid", "The retained source payload is unavailable or does not match its receipt.");
    public static readonly Error ReprocessingEnqueueFailed = new("Ingestion.ReprocessingEnqueueFailed", "The reprocessing task could not be enqueued.");
    public static Error ConnectionNotEnabled => IngestionDomainErrors.ConnectionNotEnabled;
    public static Error RunNotActive => IngestionDomainErrors.RunNotActive;
}
