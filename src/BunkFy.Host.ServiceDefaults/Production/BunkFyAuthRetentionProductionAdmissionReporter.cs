namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class BunkFyAuthRetentionProductionAdmissionReporter(
    IOptions<BunkFyAuthRetentionProductionAdmissionOptions> options,
    BunkFyAuthRetentionProductionAdmissionRegistration registration,
    ILogger<BunkFyAuthRetentionProductionAdmissionReporter> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        BunkFyAuthRetentionProductionAdmissionOptions approved = options.Value;
        logger.LogInformation(
            "Auth retention production admission passed for {HostSurface}. Maintenance owner: {MaintenanceOwner}; current process owns maintenance: {CurrentProcessOwnsMaintenance}; owner instance count: {OwnerInstanceCount}; approval reference: {ApprovalReference}; existing history disposition: {ExistingHistoryDisposition}; session history days: {SessionHistoryDays}; authentication failure history hours: {AuthenticationFailureHistoryHours}.",
            registration.HostSurface,
            approved.MaintenanceOwner,
            approved.CurrentProcessOwnsMaintenance,
            approved.MaintenanceOwnerInstanceCount,
            approved.ApprovalReference,
            approved.ExistingHistoryDisposition,
            approved.SessionHistoryDays,
            approved.AuthenticationFailureHistoryHours);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
