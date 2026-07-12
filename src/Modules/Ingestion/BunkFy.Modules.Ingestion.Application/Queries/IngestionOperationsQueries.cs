namespace BunkFy.Modules.Ingestion.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Contracts;

public sealed record GetAdapterConnectionQuery(Guid PropertyId, Guid ConnectionId)
    : IQuery<AdapterConnectionDto>;

public sealed record ListAdapterTypeCapabilitiesQuery : IQuery<AdapterTypeCapabilityListResponse>;

public sealed record ListObservationParserCapabilitiesQuery : IQuery<ObservationParserCapabilityListResponse>;

public sealed record GetAdapterConnectionHealthQuery(Guid PropertyId, Guid ConnectionId)
    : IQuery<AdapterConnectionHealthDto>;

public sealed record ListAdapterConnectionsQuery(
    Guid PropertyId,
    AdapterConnectionStatus? Status,
    int Page,
    int PageSize) : IQuery<AdapterConnectionListResponse>;

public sealed record GetIngestionRunQuery(Guid PropertyId, Guid RunId)
    : IQuery<IngestionRunDto>;

public sealed record ListIngestionRunsQuery(
    Guid PropertyId,
    Guid? ConnectionId,
    IngestionRunStatus? Status,
    int Page,
    int PageSize) : IQuery<IngestionRunListResponse>;

public sealed record GetObservationReceiptQuery(Guid PropertyId, Guid ReceiptId)
    : IQuery<ObservationReceiptDto>;

public sealed record GetObservationRawPayloadQuery(Guid PropertyId, Guid ReceiptId)
    : IQuery<ObservationRawPayload>;

public sealed record ObservationRawPayload(
    string ContentType,
    string ContentSha256,
    ReadOnlyMemory<byte> Content);

public sealed record ListObservationReceiptsQuery(
    Guid PropertyId,
    Guid? ConnectionId,
    Guid? RunId,
    ObservationReceiptStatus? Status,
    int Page,
    int PageSize) : IQuery<ObservationReceiptListResponse>;

public sealed record GetObservationReprocessingAttemptQuery(Guid PropertyId, Guid AttemptId)
    : IQuery<ObservationReprocessingAttemptDetailsDto>;

public sealed record ListObservationReprocessingAttemptsQuery(
    Guid PropertyId,
    Guid? SourceReceiptId,
    ObservationReprocessingStatus? Status,
    int Page,
    int PageSize) : IQuery<ObservationReprocessingAttemptListResponse>;
