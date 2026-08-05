namespace BunkFy.Extensions.DataRights.TenantTermination;

using BunkFy.Modules.DataRights.Contracts;
using Microsoft.Extensions.Options;

internal sealed class TenantTerminationProductionAdmissionValidator(
    TenantTerminationProductionAdmissionRegistration registration)
    : IValidateOptions<TenantTerminationProductionAdmissionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        TenantTerminationProductionAdmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.ExecutionEnabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        if (!registration.DataRightsComposed)
        {
            failures.Add(
                "The tenant-termination execution worker must compose Data Rights.");
        }

        if (!registration.CompleteOwnerTopology)
        {
            failures.Add(
                "The tenant-termination execution worker must compose the complete owner topology.");
        }

        if (!registration.TaskWorkerEnabled)
        {
            failures.Add(
                "Tasks:Worker:Enabled must be true for tenant termination.");
        }

        if (!registration.WorkerGroups.Contains(
                DataRightsModuleMetadata.TenantTerminationWorkerGroup,
                StringComparer.Ordinal))
        {
            failures.Add(
                $"Tasks:Worker:WorkerGroups must contain {DataRightsModuleMetadata.TenantTerminationWorkerGroup}.");
        }

        if (registration.IsProduction)
        {
            ValidateProductionEvidence(options, failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateProductionEvidence(
        TenantTerminationProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.ApprovalState !=
            TenantTerminationApprovalState.Approved)
        {
            failures.Add(
                $"{TenantTerminationProductionAdmissionOptions.SectionName}:ApprovalState must be Approved when execution is enabled in Production.");
        }

        ValidateReference(
            options.ApprovalReference,
            nameof(options.ApprovalReference),
            failures);
        ValidateReference(
            options.BackupEvidenceReference,
            nameof(options.BackupEvidenceReference),
            failures);
        ValidateReference(
            options.RestoreDrillEvidenceReference,
            nameof(options.RestoreDrillEvidenceReference),
            failures);
        ValidateReference(
            options.OperatorAssuranceReference,
            nameof(options.OperatorAssuranceReference),
            failures);
        if (!TenantTerminationApprovalEvidenceContract.IsSha256(
                options.OwnerCatalogSha256))
        {
            failures.Add(
                $"{TenantTerminationProductionAdmissionOptions.SectionName}:OwnerCatalogSha256 must be a lowercase SHA-256 digest.");
        }
    }

    private static void ValidateReference(
        string? value,
        string name,
        List<string> failures)
    {
        if (!TenantTerminationApprovalEvidenceContract.IsReference(value))
        {
            failures.Add(
                $"{TenantTerminationProductionAdmissionOptions.SectionName}:{name} must be a 3-128 character non-secret evidence identifier.");
        }
    }

}

internal sealed record TenantTerminationProductionAdmissionRegistration(
    bool IsProduction,
    bool DataRightsComposed,
    bool CompleteOwnerTopology,
    bool TaskWorkerEnabled,
    IReadOnlySet<string> WorkerGroups);
