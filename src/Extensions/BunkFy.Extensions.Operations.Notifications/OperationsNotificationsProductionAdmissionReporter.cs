namespace BunkFy.Extensions.Operations.Notifications;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class OperationsNotificationsProductionAdmissionReporter(
    OperationsNotificationsProductionAdmissionRegistration registration,
    OperationsNotificationsPersonalDataCatalogEvidence catalog,
    IOptions<OperationsNotificationsProductionAdmissionOptions> options,
    ILogger<OperationsNotificationsProductionAdmissionReporter> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        OperationsNotificationsProductionAdmissionOptions admission =
            options.Value;
        logger.LogInformation(
            "Operations Notifications production admission accepted. HostRole={HostRole}; RetentionOwner={RetentionOwner}; RetentionOwnerInstanceCount={RetentionOwnerInstanceCount}; CatalogVersion={CatalogVersion}; CatalogSha256={CatalogSha256}; ApprovalReference={ApprovalReference}; LegacyHistoryDisposition={LegacyHistoryDisposition}; ReadHistoryDays={ReadHistoryDays}; UnreadHistoryDays={UnreadHistoryDays}; BroadcastDays={BroadcastDays}; DeliveryAttemptDays={DeliveryAttemptDays}.",
            registration.HostRole,
            admission.RetentionOwner,
            admission.RetentionOwnerInstanceCount,
            catalog.Document.CatalogVersion,
            catalog.ContentSha256,
            admission.ApprovalReference,
            admission.LegacyHistoryDisposition,
            admission.ReadHistoryDays,
            admission.UnreadHistoryDays,
            admission.BroadcastDays,
            admission.DeliveryAttemptDays);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
