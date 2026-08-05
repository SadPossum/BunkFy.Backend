namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class TenantTerminationProcess
{
    public Result CompleteFreeze(
        long freezeOperationRevision,
        long workspaceFenceRevision,
        string frozenRevisionSha256,
        IReadOnlyCollection<TenantTerminationFrozenOwnerDescriptor> frozenOwners,
        long expectedVersion,
        string actorId,
        DateTimeOffset frozenAtUtc)
    {
        ArgumentNullException.ThrowIfNull(frozenOwners);

        Result<IReadOnlyList<TenantTerminationFrozenOwner>> prepared =
            PrepareFrozenOwners(frozenOwners);
        if (prepared.IsFailure)
        {
            return Result.Failure(prepared.Error);
        }

        string normalizedActor = NormalizeActor(actorId);
        if (this.IsExactFreezeCheckpoint(
                freezeOperationRevision,
                workspaceFenceRevision,
                frozenRevisionSha256,
                prepared.Value,
                normalizedActor,
                frozenAtUtc))
        {
            return Result.Success();
        }

        Result ready = this.ValidateChange(
            expectedVersion,
            actorId,
            frozenAtUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (this.Phase != TenantTerminationProcessPhase.Freeze ||
            this.Status != TenantTerminationProcessStatus.Running ||
            freezeOperationRevision != this.OperationRevision ||
            freezeOperationRevision <= this.ApprovalRevision ||
            workspaceFenceRevision <= 0 ||
            !IsSha256(frozenRevisionSha256) ||
            this.HasFreezeCheckpoint())
        {
            return Result.Failure(
                DataRightsDomainErrors.TenantTerminationFreezeCheckpointInvalid);
        }

        this.FreezeOperationRevision = freezeOperationRevision;
        this.WorkspaceFenceRevision = workspaceFenceRevision;
        this.FrozenRevisionSha256 = frozenRevisionSha256;
        this.FrozenBy = normalizedActor;
        this.FrozenAtUtc = frozenAtUtc;
        this.frozenExportOwners.AddRange(prepared.Value);
        this.ClearOutcome();
        this.Phase = this.ExportRequested
            ? TenantTerminationProcessPhase.Export
            : TenantTerminationProcessPhase.Destroy;
        this.Status = TenantTerminationProcessStatus.Pending;
        this.CompleteChange(normalizedActor, frozenAtUtc);
        return Result.Success();
    }

    private bool IsExactFreezeCheckpoint(
        long freezeOperationRevision,
        long workspaceFenceRevision,
        string frozenRevisionSha256,
        IReadOnlyList<TenantTerminationFrozenOwner> frozenOwners,
        string actorId,
        DateTimeOffset frozenAtUtc)
    {
        TenantTerminationProcessPhase expectedPhase = this.ExportRequested
            ? TenantTerminationProcessPhase.Export
            : TenantTerminationProcessPhase.Destroy;
        return this.Phase == expectedPhase &&
            this.Status == TenantTerminationProcessStatus.Pending &&
            this.FreezeOperationRevision == freezeOperationRevision &&
            this.WorkspaceFenceRevision == workspaceFenceRevision &&
            string.Equals(
                this.FrozenRevisionSha256,
                frozenRevisionSha256,
                StringComparison.Ordinal) &&
            string.Equals(this.FrozenBy, actorId, StringComparison.Ordinal) &&
            this.FrozenAtUtc == frozenAtUtc &&
            this.frozenExportOwners.Count == frozenOwners.Count &&
            this.frozenExportOwners
                .OrderBy(owner => owner.Ordinal)
                .Zip(
                    frozenOwners,
                    (persisted, supplied) =>
                        persisted.Ordinal == supplied.Ordinal &&
                        persisted.OwnerKey == supplied.OwnerKey &&
                        persisted.ContractVersion == supplied.ContractVersion &&
                        persisted.CatalogVersion == supplied.CatalogVersion &&
                        persisted.CatalogSha256 == supplied.CatalogSha256)
                .All(matches => matches);
    }

    private bool HasFreezeCheckpoint() =>
        this.FreezeOperationRevision.HasValue ||
        this.WorkspaceFenceRevision.HasValue ||
        this.FrozenRevisionSha256 is not null ||
        this.FrozenBy is not null ||
        this.FrozenAtUtc.HasValue ||
        this.frozenExportOwners.Count > 0;

    private static Result<IReadOnlyList<TenantTerminationFrozenOwner>>
        PrepareFrozenOwners(
            IReadOnlyCollection<TenantTerminationFrozenOwnerDescriptor> owners)
    {
        if (owners.Count is <= 0 or > MaximumFrozenOwners)
        {
            return Result.Failure<
                IReadOnlyList<TenantTerminationFrozenOwner>>(
                    DataRightsDomainErrors
                        .TenantTerminationFreezeCheckpointInvalid);
        }

        TenantTerminationFrozenOwnerDescriptor[] ordered = owners
            .OrderBy(owner => owner?.OwnerKey, StringComparer.Ordinal)
            .ToArray();
        List<TenantTerminationFrozenOwner> prepared = [];
        HashSet<string> ownerKeys = new(StringComparer.Ordinal);
        for (int index = 0; index < ordered.Length; index++)
        {
            TenantTerminationFrozenOwnerDescriptor? descriptor = ordered[index];
            if (descriptor is null ||
                !ownerKeys.Add(descriptor.OwnerKey?.Trim() ?? string.Empty))
            {
                return Result.Failure<
                    IReadOnlyList<TenantTerminationFrozenOwner>>(
                        DataRightsDomainErrors
                            .TenantTerminationFreezeCheckpointInvalid);
            }

            Result<TenantTerminationFrozenOwner> owner =
                TenantTerminationFrozenOwner.Create(index + 1, descriptor);
            if (owner.IsFailure)
            {
                return Result.Failure<
                    IReadOnlyList<TenantTerminationFrozenOwner>>(owner.Error);
            }

            prepared.Add(owner.Value);
        }

        return Result.Success<
            IReadOnlyList<TenantTerminationFrozenOwner>>(prepared);
    }
}
