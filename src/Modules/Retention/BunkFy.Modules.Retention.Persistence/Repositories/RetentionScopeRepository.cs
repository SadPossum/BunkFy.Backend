namespace BunkFy.Modules.Retention.Persistence.Repositories;

using System.Runtime.CompilerServices;
using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Contracts;
using Microsoft.EntityFrameworkCore;

internal sealed class RetentionScopeRepository(RetentionDbContext dbContext)
    : IRetentionScopeRepository
{
    public async Task ApplyOrganizationAsync(
        RetentionOrganizationWriteModel organization,
        CancellationToken cancellationToken)
    {
        RetentionTenantProjection? current = dbContext.TenantProjections.Local
            .FirstOrDefault(item => item.ScopeId == organization.ScopeId) ??
            await dbContext.TenantProjections.SingleOrDefaultAsync(
                item => item.ScopeId == organization.ScopeId,
                cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            dbContext.TenantProjections.Add(new(
                organization.ScopeId,
                organization.OrganizationId,
                organization.IsActive,
                organization.SourceVersion));
            return;
        }

        current.Apply(
            organization.OrganizationId,
            organization.IsActive,
            organization.SourceVersion);
    }

    public async Task ApplyPropertyTopologyAsync(
        RetentionPropertyTopologyWriteModel property,
        CancellationToken cancellationToken)
    {
        RetentionPropertyProjection current = await this.GetOrCreatePropertyAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        current.ApplyTopology(property.IsActive, property.SourceVersion);
    }

    public async Task ApplyPropertyPolicyAsync(
        RetentionPropertyPolicyWriteModel property,
        CancellationToken cancellationToken)
    {
        RetentionPropertyProjection current = await this.GetOrCreatePropertyAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        current.ApplyPolicy(
            property.IsProcessingEnabled,
            property.RetentionPolicyVersion,
            property.SourceVersion);
    }

    public async Task<IReadOnlyList<RetentionScheduleTarget>> ListActiveTargetsAsync(
        RetentionTargetScopeKind targetKind,
        CancellationToken cancellationToken)
    {
        IQueryable<RetentionScheduleTarget>? query = this.ActiveTargets(targetKind);
        return query is null
            ? []
            : await query.ToArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<RetentionScheduleTarget> StreamActiveTargetsAsync(
        RetentionTargetScopeKind targetKind,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IQueryable<RetentionScheduleTarget>? query = this.ActiveTargets(targetKind);
        if (query is null)
        {
            yield break;
        }

        await foreach (RetentionScheduleTarget target in query
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return target;
        }
    }

    public async Task<IReadOnlyList<RetentionScheduleTarget>>
        ListCurrentActiveTargetsAsync(
            RetentionTargetScopeKind targetKind,
            CancellationToken cancellationToken) =>
        targetKind switch
        {
            RetentionTargetScopeKind.Tenant =>
                await dbContext.TenantProjections
                    .AsNoTracking()
                    .Where(tenant =>
                        tenant.IsActive &&
                        !dbContext.TenantRevisions.Any(state =>
                            state.LifecycleStatus !=
                                RetentionTenantLifecycleStatus.Open))
                    .OrderBy(tenant => tenant.ScopeId)
                    .Select(tenant => new RetentionScheduleTarget(
                        tenant.ScopeId,
                        null))
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false),
            RetentionTargetScopeKind.Property =>
                await (
                    from property in dbContext.PropertyProjections.AsNoTracking()
                    join tenant in dbContext.TenantProjections.AsNoTracking()
                        on property.ScopeId equals tenant.ScopeId
                    where tenant.IsActive &&
                        !dbContext.TenantRevisions.Any(state =>
                            state.LifecycleStatus !=
                                RetentionTenantLifecycleStatus.Open) &&
                        property.IsKnown &&
                        property.IsActive &&
                        property.IsProcessingEnabled &&
                        property.RetentionPolicyVersion > 0
                    orderby property.Id
                    select new RetentionScheduleTarget(
                        property.ScopeId,
                        property.Id))
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false),
            _ => []
        };

    public async Task<bool> IsActiveTargetAsync(
        RetentionTargetScopeKind targetKind,
        Guid? propertyId,
        CancellationToken cancellationToken)
    {
        bool tenantClosing = await dbContext.TenantRevisions
            .AnyAsync(
                state => state.LifecycleStatus !=
                    RetentionTenantLifecycleStatus.Open,
                cancellationToken)
            .ConfigureAwait(false);
        if (tenantClosing)
        {
            return false;
        }

        bool tenantActive = await dbContext.TenantProjections
            .AnyAsync(tenant => tenant.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantActive)
        {
            return false;
        }

        return targetKind switch
        {
            RetentionTargetScopeKind.Tenant => propertyId is null,
            RetentionTargetScopeKind.Property => propertyId is not null &&
                await dbContext.PropertyProjections.AnyAsync(
                    property =>
                        property.Id == propertyId &&
                        property.IsKnown &&
                        property.IsActive &&
                        property.IsProcessingEnabled &&
                        property.RetentionPolicyVersion > 0,
                    cancellationToken).ConfigureAwait(false),
            _ => false
        };
    }

    private async Task<RetentionPropertyProjection> GetOrCreatePropertyAsync(
        string scopeId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        RetentionPropertyProjection? current = dbContext.PropertyProjections.Local
            .FirstOrDefault(item =>
                item.ScopeId == scopeId &&
                item.Id == propertyId) ??
            await dbContext.PropertyProjections.SingleOrDefaultAsync(
                item => item.Id == propertyId,
                cancellationToken).ConfigureAwait(false);
        if (current is not null)
        {
            return current;
        }

        current = new RetentionPropertyProjection(
            scopeId,
            propertyId,
            false,
            0);
        dbContext.PropertyProjections.Add(current);
        return current;
    }

    private IQueryable<RetentionScheduleTarget>? ActiveTargets(
        RetentionTargetScopeKind targetKind) => targetKind switch
        {
            RetentionTargetScopeKind.Tenant => dbContext.TenantProjections
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(tenant =>
                    tenant.IsActive &&
                    !dbContext.TenantRevisions
                        .IgnoreQueryFilters()
                        .Any(state =>
                            state.ScopeId == tenant.ScopeId &&
                            state.LifecycleStatus !=
                                RetentionTenantLifecycleStatus.Open))
                .OrderBy(tenant => tenant.ScopeId)
                .Select(tenant => new RetentionScheduleTarget(
                    tenant.ScopeId,
                    null)),
            RetentionTargetScopeKind.Property =>
                from property in dbContext.PropertyProjections
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                join tenant in dbContext.TenantProjections
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    on property.ScopeId equals tenant.ScopeId
                where tenant.IsActive &&
                    !dbContext.TenantRevisions
                        .IgnoreQueryFilters()
                        .Any(state =>
                            state.ScopeId == tenant.ScopeId &&
                            state.LifecycleStatus !=
                                RetentionTenantLifecycleStatus.Open) &&
                    property.IsKnown &&
                    property.IsActive &&
                    property.IsProcessingEnabled &&
                    property.RetentionPolicyVersion > 0
                orderby property.ScopeId, property.Id
                select new RetentionScheduleTarget(
                    property.ScopeId,
                    property.Id),
            _ => null
        };
}
