namespace BunkFy.Modules.Retention.Application.Ports;

using System.Runtime.CompilerServices;
using BunkFy.Modules.Retention.Contracts;

public interface IRetentionScopeRepository
{
    Task ApplyOrganizationAsync(
        RetentionOrganizationWriteModel organization,
        CancellationToken cancellationToken);

    Task ApplyPropertyTopologyAsync(
        RetentionPropertyTopologyWriteModel property,
        CancellationToken cancellationToken);

    Task ApplyPropertyPolicyAsync(
        RetentionPropertyPolicyWriteModel property,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RetentionScheduleTarget>> ListActiveTargetsAsync(
        RetentionTargetScopeKind targetKind,
        CancellationToken cancellationToken);

    async IAsyncEnumerable<RetentionScheduleTarget> StreamActiveTargetsAsync(
        RetentionTargetScopeKind targetKind,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<RetentionScheduleTarget> targets =
            await this.ListActiveTargetsAsync(targetKind, cancellationToken)
                .ConfigureAwait(false);
        foreach (RetentionScheduleTarget target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return target;
        }
    }

    Task<IReadOnlyList<RetentionScheduleTarget>> ListCurrentActiveTargetsAsync(
        RetentionTargetScopeKind targetKind,
        CancellationToken cancellationToken);

    Task<bool> IsActiveTargetAsync(
        RetentionTargetScopeKind targetKind,
        Guid? propertyId,
        CancellationToken cancellationToken);
}

public sealed record RetentionOrganizationWriteModel(
    string ScopeId,
    Guid OrganizationId,
    bool IsActive,
    long SourceVersion);

public sealed record RetentionPropertyTopologyWriteModel(
    string ScopeId,
    Guid PropertyId,
    bool IsActive,
    long SourceVersion);

public sealed record RetentionPropertyPolicyWriteModel(
    string ScopeId,
    Guid PropertyId,
    bool IsProcessingEnabled,
    int RetentionPolicyVersion,
    long SourceVersion);

public sealed record RetentionScheduleTarget(
    string ScopeId,
    Guid? PropertyId);
