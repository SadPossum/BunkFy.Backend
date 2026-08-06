namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class BunkFyProductionDeploymentReporter(
    IOptions<BunkFyDeploymentOptions> options,
    BunkFyDeploymentSurfaceRegistration registration,
    ILogger<BunkFyProductionDeploymentReporter> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        BunkFyDeploymentOptions approved = options.Value;
        logger.LogInformation(
            "Production deployment declaration accepted for {Surface}. Profile: {Profile}; runtime: {Runtime}; admission evidence reference: {AdmissionEvidenceReference}.",
            registration.Surface,
            approved.Profile,
            approved.Runtime,
            approved.AdmissionEvidenceReference);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
