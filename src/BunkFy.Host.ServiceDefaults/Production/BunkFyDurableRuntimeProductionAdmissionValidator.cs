namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Options;

internal sealed class BunkFyDurableRuntimeProductionAdmissionValidator(
    BunkFyDurableRuntimeProductionAdmissionRegistration registration,
    BunkFyDurableRuntimeRuntimeOptions runtime)
    : IValidateOptions<BunkFyDurableRuntimeProductionAdmissionOptions>
{
    private static readonly TimeSpan MaximumRetention = TimeSpan.FromDays(3650);

    public ValidateOptionsResult Validate(
        string? name,
        BunkFyDurableRuntimeProductionAdmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!registration.IsProduction)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        ValidateApproval(options, failures);
        ValidateOwner(options, failures);
        this.ValidateComposition(failures);
        this.ValidateRuntime(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateApproval(
        BunkFyDurableRuntimeProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.ApprovalState != BunkFyDurableRuntimeApprovalState.Approved)
        {
            failures.Add(
                $"{BunkFyDurableRuntimeProductionAdmissionOptions.SectionName}:ApprovalState must be Approved in Production.");
        }

        if (!BunkFyProductionAdmissionReference.IsValid(options.ApprovalReference))
        {
            failures.Add(
                $"{BunkFyDurableRuntimeProductionAdmissionOptions.SectionName}:ApprovalReference must be a 3-128 character non-secret evidence identifier.");
        }

        ValidateWindow(options.ProcessedOutboxRetention, nameof(options.ProcessedOutboxRetention), failures);
        ValidateWindow(options.ProcessedInboxRetention, nameof(options.ProcessedInboxRetention), failures);
        ValidateWindow(options.BrokerReplayHorizon, nameof(options.BrokerReplayHorizon), failures);
        ValidateWindow(options.SucceededRunRetention, nameof(options.SucceededRunRetention), failures);
        ValidateWindow(options.FailedRunRetention, nameof(options.FailedRunRetention), failures);
        ValidateWindow(options.CanceledRunRetention, nameof(options.CanceledRunRetention), failures);
        ValidateWindow(options.TimedOutRunRetention, nameof(options.TimedOutRunRetention), failures);
        ValidateWindow(options.HandledControlRetention, nameof(options.HandledControlRetention), failures);
        ValidateWindow(options.FailedControlRetention, nameof(options.FailedControlRetention), failures);
        ValidateWindow(options.ExpiredControlRetention, nameof(options.ExpiredControlRetention), failures);

        if (options.ProcessedInboxRetention < options.BrokerReplayHorizon)
        {
            failures.Add(
                $"{BunkFyDurableRuntimeProductionAdmissionOptions.SectionName}:ProcessedInboxRetention must be greater than or equal to BrokerReplayHorizon.");
        }
    }

    private static void ValidateOwner(
        BunkFyDurableRuntimeProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.MaintenanceOwner !=
            BunkFyDurableRuntimeMaintenanceOwner.Worker)
        {
            failures.Add(
                $"{BunkFyDurableRuntimeProductionAdmissionOptions.SectionName}:MaintenanceOwner must be Worker.");
        }

        if (options.MaintenanceOwnerInstanceCount != 1)
        {
            failures.Add(
                $"{BunkFyDurableRuntimeProductionAdmissionOptions.SectionName}:MaintenanceOwnerInstanceCount must be exactly 1.");
        }
    }

    private void ValidateComposition(List<string> failures)
    {
        if (registration.TaskWorkerEnabled &&
            !registration.TaskRuntimeComposed)
        {
            failures.Add(
                "Tasks:Worker:Enabled requires Worker:Modules:TaskRuntime=true.");
        }

        if (registration.TaskSchedulerEnabled &&
            (!registration.TaskWorkerEnabled ||
             !registration.TaskRuntimeComposed))
        {
            failures.Add(
                "Tasks:Scheduler:Enabled requires task execution and the TaskRuntime module.");
        }

        if (registration.NatsConsumersEnabled &&
            !registration.NatsPublishingEnabled)
        {
            failures.Add(
                "NatsConsumers:Enabled requires NatsJetStream:Enabled=true.");
        }

        if (runtime.TaskRetentionEnabled &&
            !registration.TaskRuntimeComposed)
        {
            failures.Add(
                "TaskRuntimeRetention:Enabled requires the TaskRuntime module in the current process.");
        }
    }

    private void ValidateRuntime(
        BunkFyDurableRuntimeProductionAdmissionOptions options,
        List<string> failures)
    {
        bool ownsMaintenance = options.CurrentProcessOwnsMaintenance;
        if (ownsMaintenance &&
            registration.HostRole != BunkFyDurableRuntimeHostRole.Worker)
        {
            failures.Add(
                "Only a BunkFy Worker process may own durable-runtime maintenance.");
        }

        if (ownsMaintenance && !registration.TaskRuntimeComposed)
        {
            failures.Add(
                "The durable-runtime maintenance owner must compose TaskRuntime.");
        }

        CompareEnabled(
            ownsMaintenance,
            runtime.MessageJournalCleanupEnabled,
            "MessageJournalCleanup:Enabled",
            failures);
        CompareEnabled(
            ownsMaintenance,
            runtime.TaskRetentionEnabled,
            "TaskRuntimeRetention:Enabled",
            failures);

        if (ownsMaintenance &&
            (!runtime.CleanupProcessedOutbox ||
             !runtime.CleanupProcessedInbox))
        {
            failures.Add(
                "The durable-runtime maintenance owner must clean both processed outbox and inbox journals.");
        }

        CompareWindow(options.ProcessedOutboxRetention, runtime.ProcessedOutboxRetention, "MessageJournalCleanup:ProcessedOutboxRetention", failures);
        CompareWindow(options.ProcessedInboxRetention, runtime.ProcessedInboxRetention, "MessageJournalCleanup:ProcessedInboxRetention", failures);
        CompareWindow(options.BrokerReplayHorizon, runtime.BrokerReplayHorizon, "MessageJournalCleanup:BrokerReplayHorizon", failures);

        if (!registration.TaskRuntimeComposed)
        {
            return;
        }

        CompareWindow(options.SucceededRunRetention, runtime.SucceededRunRetention, "TaskRuntimeRetention:SucceededRunRetention", failures);
        CompareWindow(options.FailedRunRetention, runtime.FailedRunRetention, "TaskRuntimeRetention:FailedRunRetention", failures);
        CompareWindow(options.CanceledRunRetention, runtime.CanceledRunRetention, "TaskRuntimeRetention:CanceledRunRetention", failures);
        CompareWindow(options.TimedOutRunRetention, runtime.TimedOutRunRetention, "TaskRuntimeRetention:TimedOutRunRetention", failures);
        CompareWindow(options.HandledControlRetention, runtime.HandledControlRetention, "TaskRuntimeRetention:HandledControlRetention", failures);
        CompareWindow(options.FailedControlRetention, runtime.FailedControlRetention, "TaskRuntimeRetention:FailedControlRetention", failures);
        CompareWindow(options.ExpiredControlRetention, runtime.ExpiredControlRetention, "TaskRuntimeRetention:ExpiredControlRetention", failures);
    }

    private static void CompareEnabled(
        bool expected,
        bool actual,
        string configurationKey,
        List<string> failures)
    {
        if (expected != actual)
        {
            failures.Add(
                $"{configurationKey} must be {expected.ToString().ToLowerInvariant()} for this process role.");
        }
    }

    private static void CompareWindow(
        TimeSpan approved,
        TimeSpan configured,
        string configurationKey,
        List<string> failures)
    {
        if (approved != configured)
        {
            failures.Add(
                $"{configurationKey} must equal the approved value {approved:c}.");
        }
    }

    private static void ValidateWindow(
        TimeSpan value,
        string name,
        List<string> failures)
    {
        if (value <= TimeSpan.Zero || value > MaximumRetention)
        {
            failures.Add(
                $"{BunkFyDurableRuntimeProductionAdmissionOptions.SectionName}:{name} must be positive and no greater than {MaximumRetention.TotalDays:0} days.");
        }
    }

}

internal sealed record BunkFyDurableRuntimeProductionAdmissionRegistration(
    bool IsProduction,
    BunkFyDurableRuntimeHostRole HostRole,
    bool TaskRuntimeComposed,
    bool TaskWorkerEnabled,
    bool TaskSchedulerEnabled,
    bool NatsPublishingEnabled,
    bool NatsConsumersEnabled);

internal sealed record BunkFyDurableRuntimeRuntimeOptions(
    bool MessageJournalCleanupEnabled,
    bool CleanupProcessedOutbox,
    bool CleanupProcessedInbox,
    TimeSpan ProcessedOutboxRetention,
    TimeSpan ProcessedInboxRetention,
    TimeSpan BrokerReplayHorizon,
    bool TaskRetentionEnabled,
    TimeSpan SucceededRunRetention,
    TimeSpan FailedRunRetention,
    TimeSpan CanceledRunRetention,
    TimeSpan TimedOutRunRetention,
    TimeSpan HandledControlRetention,
    TimeSpan FailedControlRetention,
    TimeSpan ExpiredControlRetention);
