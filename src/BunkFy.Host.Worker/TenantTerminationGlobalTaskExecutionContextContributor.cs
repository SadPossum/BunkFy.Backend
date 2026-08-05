namespace BunkFy.Host.Worker;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy;

internal sealed class TenantTerminationGlobalTaskExecutionContextContributor(
    ITenantContextAccessor tenantContext)
    : ITaskExecutionContextContributor
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    private bool established;

    public ValueTask<TaskExecutionContextPreparationResult> PrepareAsync(
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.established = false;
        if (!IsGlobalTerminationRegistration(context))
        {
            return ValueTask.FromResult(
                TaskExecutionContextPreparationResult.Success());
        }

        if (!TryReadPayload(
                context,
                out ExecuteGlobalTenantTerminationOwnerWorkPayload? payload) ||
            !TryTenantId(payload.TenantId, out string tenantId) ||
            context.Lease.ScopeId is not null ||
            context.Lease.CorrelationId != payload.ProcessId)
        {
            return ValueTask.FromResult(
                TaskExecutionContextPreparationResult.Failure(
                    "Global tenant-termination task coordinates are invalid."));
        }

        tenantContext.SetTenant(tenantId);
        this.established = true;
        return ValueTask.FromResult(
            TaskExecutionContextPreparationResult.Success());
    }

    public ValueTask CleanupAsync(
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken)
    {
        if (this.established)
        {
            tenantContext.ClearTenant();
            this.established = false;
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsGlobalTerminationRegistration(
        TaskExecutionContextPreparationContext context) =>
        context.Registration.PayloadType ==
            typeof(ExecuteGlobalTenantTerminationOwnerWorkPayload) &&
        !context.Registration.IsTenantScoped() &&
        string.Equals(
            context.Registration.ModuleName,
            DataRightsModuleMetadata.Name,
            StringComparison.Ordinal) &&
        string.Equals(
            context.Registration.TaskName,
            ExecuteGlobalTenantTerminationOwnerWorkPayload.TaskName,
            StringComparison.Ordinal) &&
        context.Registration.PayloadVersion ==
            ExecuteGlobalTenantTerminationOwnerWorkPayload.PayloadVersion &&
        string.Equals(
            context.Lease.ModuleName,
            context.Registration.ModuleName,
            StringComparison.Ordinal) &&
        string.Equals(
            context.Lease.TaskName,
            context.Registration.TaskName,
            StringComparison.Ordinal) &&
        context.Lease.PayloadVersion == context.Registration.PayloadVersion;

    private static bool TryReadPayload(
        TaskExecutionContextPreparationContext context,
        out ExecuteGlobalTenantTerminationOwnerWorkPayload payload)
    {
        payload = null!;
        try
        {
            ExecuteGlobalTenantTerminationOwnerWorkPayload? candidate =
                JsonSerializer.Deserialize<
                    ExecuteGlobalTenantTerminationOwnerWorkPayload>(
                        context.Lease.PayloadJson,
                        JsonOptions);
            if (candidate is null ||
                candidate.ProcessId == Guid.Empty ||
                candidate.WorkItemId == Guid.Empty ||
                candidate.OperationRevision <= 0 ||
                candidate.Phase !=
                    TenantTerminationContributionPhase.Destroy ||
                string.IsNullOrWhiteSpace(candidate.OwnerKey))
            {
                return false;
            }

            payload = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryTenantId(string value, out string tenantId)
    {
        tenantId = string.Empty;
        if (!TenantIds.TryNormalize(value, out string? normalized) ||
            !string.Equals(value, normalized, StringComparison.Ordinal) ||
            !Guid.TryParseExact(normalized, "D", out Guid workspaceId) ||
            workspaceId == Guid.Empty ||
            !string.Equals(
                workspaceId.ToString("D"),
                normalized,
                StringComparison.Ordinal))
        {
            return false;
        }

        tenantId = normalized;
        return true;
    }
}
