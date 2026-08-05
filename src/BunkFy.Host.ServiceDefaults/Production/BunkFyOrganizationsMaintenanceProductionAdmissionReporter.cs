namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class BunkFyOrganizationsMaintenanceProductionAdmissionReporter(
    IOptions<BunkFyOrganizationsMaintenanceProductionAdmissionOptions> options,
    BunkFyOrganizationsMaintenanceProductionAdmissionRegistration registration,
    ILogger<BunkFyOrganizationsMaintenanceProductionAdmissionReporter> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions approved =
            options.Value;
        logger.LogInformation(
            "Organizations maintenance production admission passed for {HostSurface}. Maintenance owner: {MaintenanceOwner}; current process owns maintenance: {CurrentProcessOwnsMaintenance}; owner instance count: {OwnerInstanceCount}; approval reference: {ApprovalReference}; existing history disposition: {ExistingHistoryDisposition}; invitation history days: {InvitationHistoryDays}; enrollment history days: {EnrollmentHistoryDays}.",
            registration.HostSurface,
            approved.MaintenanceOwner,
            approved.CurrentProcessOwnsMaintenance,
            approved.MaintenanceOwnerInstanceCount,
            approved.ApprovalReference,
            approved.ExistingHistoryDisposition,
            approved.InvitationHistoryDays,
            approved.EnrollmentHistoryDays);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
