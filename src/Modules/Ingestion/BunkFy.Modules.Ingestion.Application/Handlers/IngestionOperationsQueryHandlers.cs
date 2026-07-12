namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Parsing;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;

internal sealed class GetAdapterConnectionQueryHandler(IIngestionOperationsReader reader)
    : IQueryHandler<GetAdapterConnectionQuery, AdapterConnectionDto>
{
    public async Task<Result<AdapterConnectionDto>> HandleAsync(GetAdapterConnectionQuery query, CancellationToken cancellationToken) =>
        await reader.GetConnectionAsync(query.PropertyId, query.ConnectionId, cancellationToken).ConfigureAwait(false) is { } value
            ? Result.Success(value)
            : Result.Failure<AdapterConnectionDto>(IngestionApplicationErrors.ConnectionNotFound);
}

internal sealed class ListAdapterTypeCapabilitiesQueryHandler(IAdapterDescriptorRegistry descriptors)
    : IQueryHandler<ListAdapterTypeCapabilitiesQuery, AdapterTypeCapabilityListResponse>
{
    public Task<Result<AdapterTypeCapabilityListResponse>> HandleAsync(
        ListAdapterTypeCapabilitiesQuery query,
        CancellationToken cancellationToken)
    {
        AdapterTypeCapabilityDto[] values = descriptors.GetAll().Select(MapCapability).ToArray();
        return Task.FromResult(Result.Success(new AdapterTypeCapabilityListResponse(values)));
    }

    private static AdapterTypeCapabilityDto MapCapability(AdapterDescriptor descriptor)
    {
        long? minimumPollingIntervalSeconds = descriptor.Polling is null
            ? null
            : Math.Max(
                AdapterConnection.MinimumPollingIntervalSeconds,
                (long)Math.Ceiling(descriptor.Polling.MinimumInterval.TotalSeconds));
        long? recommendedPollingIntervalSeconds = descriptor.Polling is null
            ? null
            : Math.Max(
                minimumPollingIntervalSeconds!.Value,
                (long)Math.Ceiling(descriptor.Polling.RecommendedInterval.TotalSeconds));
        return new AdapterTypeCapabilityDto(
            descriptor.AdapterType,
            descriptor.ProtocolVersion,
            descriptor.ConfigurationSchemaVersion,
            descriptor.ExecutionModes,
            minimumPollingIntervalSeconds,
            recommendedPollingIntervalSeconds);
    }
}

internal sealed class ListObservationParserCapabilitiesQueryHandler(IObservationParserDescriptorRegistry parsers)
    : IQueryHandler<ListObservationParserCapabilitiesQuery, ObservationParserCapabilityListResponse>
{
    public Task<Result<ObservationParserCapabilityListResponse>> HandleAsync(
        ListObservationParserCapabilitiesQuery query,
        CancellationToken cancellationToken)
    {
        ObservationParserCapabilityDto[] values = parsers.GetAll()
            .Select(descriptor => new ObservationParserCapabilityDto(
                descriptor.ParserType,
                descriptor.ParserVersion,
                descriptor.SupportedAdapterTypes,
                descriptor.SupportedSourceRecordTypes,
                descriptor.OutputRecordTypes))
            .ToArray();
        return Task.FromResult(Result.Success(new ObservationParserCapabilityListResponse(values)));
    }
}

internal sealed class ListAdapterConnectionsQueryHandler(IIngestionOperationsReader reader)
    : IQueryHandler<ListAdapterConnectionsQuery, AdapterConnectionListResponse>
{
    public async Task<Result<AdapterConnectionListResponse>> HandleAsync(
        ListAdapterConnectionsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Status is { } status && (status == AdapterConnectionStatus.Unknown || !Enum.IsDefined(status)))
        {
            return Result.Failure<AdapterConnectionListResponse>(IngestionApplicationErrors.ConnectionStatusInvalid);
        }

        return Result.Success(await reader.ListConnectionsAsync(
            query.PropertyId, query.Status, PageRequest.Normalize(query.Page, query.PageSize), cancellationToken).ConfigureAwait(false));
    }
}

internal sealed class GetAdapterConnectionHealthQueryHandler(
    IIngestionOperationsReader reader,
    IAdapterDescriptorRegistry descriptors,
    ISystemClock clock)
    : IQueryHandler<GetAdapterConnectionHealthQuery, AdapterConnectionHealthDto>
{
    public async Task<Result<AdapterConnectionHealthDto>> HandleAsync(
        GetAdapterConnectionHealthQuery query,
        CancellationToken cancellationToken) =>
        await reader.GetConnectionHealthAsync(
            query.PropertyId,
            query.ConnectionId,
            clock.UtcNow,
            cancellationToken).ConfigureAwait(false) is { } value
            ? Result.Success(WithCapability(value, descriptors))
            : Result.Failure<AdapterConnectionHealthDto>(IngestionApplicationErrors.ConnectionNotFound);

    private static AdapterConnectionHealthDto WithCapability(
        AdapterConnectionHealthDto health,
        IAdapterDescriptorRegistry descriptors)
    {
        if (!descriptors.TryGet(health.AdapterType, out BunkFy.Adapter.Abstractions.AdapterDescriptor? descriptor) ||
            descriptor is null)
        {
            return health with { CapabilityStatus = AdapterCapabilityStatus.AdapterTypeNotRegistered };
        }

        return health with
        {
            CapabilityStatus = descriptor.ExecutionModes.Contains(health.ExecutionMode)
                ? AdapterCapabilityStatus.Available
                : AdapterCapabilityStatus.ExecutionModeUnsupported,
            ProtocolVersion = descriptor.ProtocolVersion,
            ConfigurationSchemaVersion = descriptor.ConfigurationSchemaVersion
        };
    }
}

internal sealed class GetIngestionRunQueryHandler(IIngestionOperationsReader reader)
    : IQueryHandler<GetIngestionRunQuery, IngestionRunDto>
{
    public async Task<Result<IngestionRunDto>> HandleAsync(GetIngestionRunQuery query, CancellationToken cancellationToken) =>
        await reader.GetRunAsync(query.PropertyId, query.RunId, cancellationToken).ConfigureAwait(false) is { } value
            ? Result.Success(value)
            : Result.Failure<IngestionRunDto>(IngestionApplicationErrors.RunNotFound);
}

internal sealed class ListIngestionRunsQueryHandler(IIngestionOperationsReader reader)
    : IQueryHandler<ListIngestionRunsQuery, IngestionRunListResponse>
{
    public async Task<Result<IngestionRunListResponse>> HandleAsync(
        ListIngestionRunsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Status is { } status && (status == IngestionRunStatus.Unknown || !Enum.IsDefined(status)))
        {
            return Result.Failure<IngestionRunListResponse>(IngestionApplicationErrors.RunStatusInvalid);
        }

        return Result.Success(await reader.ListRunsAsync(
            query.PropertyId, query.ConnectionId, query.Status, PageRequest.Normalize(query.Page, query.PageSize), cancellationToken).ConfigureAwait(false));
    }
}

internal sealed class GetObservationReceiptQueryHandler(IIngestionOperationsReader reader)
    : IQueryHandler<GetObservationReceiptQuery, ObservationReceiptDto>
{
    public async Task<Result<ObservationReceiptDto>> HandleAsync(
        GetObservationReceiptQuery query,
        CancellationToken cancellationToken) =>
        await reader.GetReceiptAsync(query.PropertyId, query.ReceiptId, cancellationToken).ConfigureAwait(false) is { } value
            ? Result.Success(value)
            : Result.Failure<ObservationReceiptDto>(IngestionApplicationErrors.ReceiptNotFound);
}

internal sealed class GetObservationRawPayloadQueryHandler(
    IIngestionOperationsReader reader,
    IRawPayloadStore rawPayloads,
    IScopeContext scopeContext)
    : IQueryHandler<GetObservationRawPayloadQuery, ObservationRawPayload>
{
    public async Task<Result<ObservationRawPayload>> HandleAsync(
        GetObservationRawPayloadQuery query,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<ObservationRawPayload>(IngestionApplicationErrors.ScopeRequired);
        }

        ObservationReceiptDto? receipt = await reader.GetReceiptAsync(
            query.PropertyId,
            query.ReceiptId,
            cancellationToken).ConfigureAwait(false);
        if (receipt is null)
        {
            return Result.Failure<ObservationRawPayload>(IngestionApplicationErrors.ReceiptNotFound);
        }

        if (receipt.RawPayloadStatus == RawPayloadRetentionStatus.Purging)
        {
            return Result.Failure<ObservationRawPayload>(IngestionApplicationErrors.RawPayloadPurgeInProgress);
        }

        if (receipt.RawPayloadStatus == RawPayloadRetentionStatus.Purged)
        {
            return Result.Failure<ObservationRawPayload>(IngestionApplicationErrors.RawPayloadUnavailable);
        }

        if (receipt.RawPayloadStatus != RawPayloadRetentionStatus.Available)
        {
            return Result.Failure<ObservationRawPayload>(IngestionApplicationErrors.RawPayloadInvalid);
        }

        RawPayloadRead? raw;
        try
        {
            raw = await rawPayloads.ReadAsync(
                receipt.RawPayloadFileId,
                scopeContext.ScopeId,
                receipt.ConnectionId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return Result.Failure<ObservationRawPayload>(IngestionApplicationErrors.RawPayloadInvalid);
        }

        if (raw is null ||
            !string.Equals(raw.ContentSha256, receipt.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ObservationRawPayload>(IngestionApplicationErrors.RawPayloadInvalid);
        }

        return Result.Success(new ObservationRawPayload(raw.ContentType, raw.ContentSha256, raw.Content));
    }
}

internal sealed class ListObservationReceiptsQueryHandler(IIngestionOperationsReader reader)
    : IQueryHandler<ListObservationReceiptsQuery, ObservationReceiptListResponse>
{
    public async Task<Result<ObservationReceiptListResponse>> HandleAsync(
        ListObservationReceiptsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Status is { } status && (status == ObservationReceiptStatus.Unknown || !Enum.IsDefined(status)))
        {
            return Result.Failure<ObservationReceiptListResponse>(IngestionApplicationErrors.ReceiptStatusInvalid);
        }

        return Result.Success(await reader.ListReceiptsAsync(
            query.PropertyId,
            query.ConnectionId,
            query.RunId,
            query.Status,
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
    }
}

internal sealed class GetObservationReprocessingAttemptQueryHandler(IIngestionOperationsReader reader)
    : IQueryHandler<GetObservationReprocessingAttemptQuery, ObservationReprocessingAttemptDetailsDto>
{
    public async Task<Result<ObservationReprocessingAttemptDetailsDto>> HandleAsync(
        GetObservationReprocessingAttemptQuery query,
        CancellationToken cancellationToken)
    {
        ObservationReprocessingAttemptDto? attempt = await reader.GetReprocessingAttemptAsync(
            query.PropertyId,
            query.AttemptId,
            cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            return Result.Failure<ObservationReprocessingAttemptDetailsDto>(
                IngestionApplicationErrors.ReprocessingAttemptNotFound);
        }

        IReadOnlyCollection<ObservationReprocessingOutputDto> outputs = await reader.ListReprocessingOutputsAsync(
            attempt.AttemptId,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new ObservationReprocessingAttemptDetailsDto(attempt, outputs));
    }
}

internal sealed class ListObservationReprocessingAttemptsQueryHandler(IIngestionOperationsReader reader)
    : IQueryHandler<ListObservationReprocessingAttemptsQuery, ObservationReprocessingAttemptListResponse>
{
    public async Task<Result<ObservationReprocessingAttemptListResponse>> HandleAsync(
        ListObservationReprocessingAttemptsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Status is { } status &&
            (status == ObservationReprocessingStatus.Unknown || !Enum.IsDefined(status)))
        {
            return Result.Failure<ObservationReprocessingAttemptListResponse>(
                IngestionApplicationErrors.ReprocessingAttemptStatusInvalid);
        }

        return Result.Success(await reader.ListReprocessingAttemptsAsync(
            query.PropertyId,
            query.SourceReceiptId,
            query.Status,
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
    }
}
