namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class BunkFyDurableRuntimeProductionAdmissionReporter(
    IOptions<BunkFyDurableRuntimeProductionAdmissionOptions> options,
    BunkFyDurableRuntimeProductionAdmissionRegistration registration,
    ILogger<BunkFyDurableRuntimeProductionAdmissionReporter> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        BunkFyDurableRuntimeProductionAdmissionOptions approved = options.Value;
        logger.LogInformation(
            "Durable runtime production admission passed for {HostRole}. Maintenance owner: {MaintenanceOwner}; current process owns maintenance: {CurrentProcessOwnsMaintenance}; owner instance count: {OwnerInstanceCount}; approval reference: {ApprovalReference}.",
            registration.HostRole,
            approved.MaintenanceOwner,
            approved.CurrentProcessOwnsMaintenance,
            approved.MaintenanceOwnerInstanceCount,
            approved.ApprovalReference);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
