namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Controls;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class GetAdapterIngressTenantControlQueryHandler(
    IAdapterIngressControlRepository controls)
    : IQueryHandler<GetAdapterIngressTenantControlQuery, AdapterIngressTenantControlDto>
{
    public async Task<Result<AdapterIngressTenantControlDto>> HandleAsync(
        GetAdapterIngressTenantControlQuery query,
        CancellationToken cancellationToken)
    {
        _ = query;
        AdapterIngressTenantControl? control = await controls.GetTenantAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(AdapterIngressControlMappings.Map(control));
    }
}

internal sealed class GetAdapterIngressGlobalControlQueryHandler(
    IAdapterIngressControlRepository controls)
    : IQueryHandler<GetAdapterIngressGlobalControlQuery, AdapterIngressGlobalControlDto>
{
    public async Task<Result<AdapterIngressGlobalControlDto>> HandleAsync(
        GetAdapterIngressGlobalControlQuery query,
        CancellationToken cancellationToken)
    {
        _ = query;
        AdapterIngressGlobalControl? control = await controls.GetGlobalAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(AdapterIngressControlMappings.Map(control));
    }
}

internal sealed class SuspendAdapterIngressTenantCommandHandler(
    IAdapterIngressControlRepository controls,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<SuspendAdapterIngressTenantCommand, AdapterIngressTenantControlDto>
{
    public async Task<Result<AdapterIngressTenantControlDto>> HandleAsync(
        SuspendAdapterIngressTenantCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<AdapterIngressTenantControlDto>(IngestionApplicationErrors.ScopeRequired);
        }

        AdapterIngressTenantControl? control = await controls.GetTenantAsync(cancellationToken)
            .ConfigureAwait(false);
        if (control is null)
        {
            if (command.ExpectedVersion != 0)
            {
                return Result.Failure<AdapterIngressTenantControlDto>(
                    BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.VersionConflict);
            }

            Result<AdapterIngressTenantControl> created = AdapterIngressTenantControl.CreateSuspended(
                scopeContext.ScopeId,
                command.ReasonCode,
                command.Actor,
                clock.UtcNow);
            if (created.IsFailure)
            {
                return Result.Failure<AdapterIngressTenantControlDto>(created.Error);
            }

            controls.Add(created.Value);
            return Result.Success(AdapterIngressControlMappings.Map(created.Value));
        }

        Result suspended = control.Suspend(
            command.ExpectedVersion,
            command.ReasonCode,
            command.Actor,
            clock.UtcNow);
        return suspended.IsSuccess
            ? Result.Success(AdapterIngressControlMappings.Map(control))
            : Result.Failure<AdapterIngressTenantControlDto>(suspended.Error);
    }
}

internal sealed class ResumeAdapterIngressTenantCommandHandler(
    IAdapterIngressControlRepository controls,
    ISystemClock clock)
    : ICommandHandler<ResumeAdapterIngressTenantCommand, AdapterIngressTenantControlDto>
{
    public async Task<Result<AdapterIngressTenantControlDto>> HandleAsync(
        ResumeAdapterIngressTenantCommand command,
        CancellationToken cancellationToken)
    {
        AdapterIngressTenantControl? control = await controls.GetTenantAsync(cancellationToken)
            .ConfigureAwait(false);
        if (control is null)
        {
            return Result.Failure<AdapterIngressTenantControlDto>(
                BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.AdapterIngressTenantAlreadyActive);
        }

        Result resumed = control.Resume(
            command.ExpectedVersion,
            command.ReasonCode,
            command.Actor,
            clock.UtcNow);
        return resumed.IsSuccess
            ? Result.Success(AdapterIngressControlMappings.Map(control))
            : Result.Failure<AdapterIngressTenantControlDto>(resumed.Error);
    }
}

internal sealed class StopAdapterIngressGloballyCommandHandler(
    IAdapterIngressControlRepository controls,
    ISystemClock clock)
    : ICommandHandler<StopAdapterIngressGloballyCommand, AdapterIngressGlobalControlDto>
{
    public async Task<Result<AdapterIngressGlobalControlDto>> HandleAsync(
        StopAdapterIngressGloballyCommand command,
        CancellationToken cancellationToken)
    {
        AdapterIngressGlobalControl? control = await controls.GetGlobalAsync(cancellationToken)
            .ConfigureAwait(false);
        if (control is null)
        {
            if (command.ExpectedVersion != 0)
            {
                return Result.Failure<AdapterIngressGlobalControlDto>(
                    BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.VersionConflict);
            }

            Result<AdapterIngressGlobalControl> created = AdapterIngressGlobalControl.CreateStopped(
                command.ReasonCode,
                command.Actor,
                clock.UtcNow);
            if (created.IsFailure)
            {
                return Result.Failure<AdapterIngressGlobalControlDto>(created.Error);
            }

            controls.Add(created.Value);
            return Result.Success(AdapterIngressControlMappings.Map(created.Value));
        }

        Result stopped = control.Stop(
            command.ExpectedVersion,
            command.ReasonCode,
            command.Actor,
            clock.UtcNow);
        return stopped.IsSuccess
            ? Result.Success(AdapterIngressControlMappings.Map(control))
            : Result.Failure<AdapterIngressGlobalControlDto>(stopped.Error);
    }
}

internal sealed class ResumeAdapterIngressGloballyCommandHandler(
    IAdapterIngressControlRepository controls,
    ISystemClock clock)
    : ICommandHandler<ResumeAdapterIngressGloballyCommand, AdapterIngressGlobalControlDto>
{
    public async Task<Result<AdapterIngressGlobalControlDto>> HandleAsync(
        ResumeAdapterIngressGloballyCommand command,
        CancellationToken cancellationToken)
    {
        AdapterIngressGlobalControl? control = await controls.GetGlobalAsync(cancellationToken)
            .ConfigureAwait(false);
        if (control is null)
        {
            return Result.Failure<AdapterIngressGlobalControlDto>(
                BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.AdapterIngressGlobalAlreadyActive);
        }

        Result resumed = control.Resume(
            command.ExpectedVersion,
            command.ReasonCode,
            command.Actor,
            clock.UtcNow);
        return resumed.IsSuccess
            ? Result.Success(AdapterIngressControlMappings.Map(control))
            : Result.Failure<AdapterIngressGlobalControlDto>(resumed.Error);
    }
}

internal static class AdapterIngressControlMappings
{
    public static AdapterIngressTenantControlDto Map(AdapterIngressTenantControl? control) =>
        control is null
            ? new(false, null, null, null, null, null, 0)
            : new(
                control.IsSuspended,
                control.LastReasonCode,
                control.LastChangedBy,
                control.LastChangedAtUtc,
                control.SuspendedAtUtc,
                control.ResumedAtUtc,
                control.Version);

    public static AdapterIngressGlobalControlDto Map(AdapterIngressGlobalControl? control) =>
        control is null
            ? new(false, null, null, null, null, null, 0)
            : new(
                control.IsStopped,
                control.LastReasonCode,
                control.LastChangedBy,
                control.LastChangedAtUtc,
                control.StoppedAtUtc,
                control.ResumedAtUtc,
                control.Version);
}
