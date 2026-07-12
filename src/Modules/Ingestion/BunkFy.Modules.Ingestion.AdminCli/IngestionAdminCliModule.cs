namespace BunkFy.Modules.Ingestion.AdminCli;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Modules.TaskRuntime.Application.Commands;
using Gma.Modules.TaskRuntime.Application.Queries;
using BunkFy.Modules.Ingestion.Admin.Contracts;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class IngestionAdminCliModule : IAdminCliModule
{
    public string Name => IngestionModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(IngestionProfiles.Default, "BunkFy.Modules.Ingestion.AdminCli");
        builder.Services.AddIngestionApplication();
        builder.AddIngestionPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command adapterTypes = new("adapter-types", "Inspect registered adapter capabilities.")
        {
            CreateAdapterTypeListCommand(commands.Services, globalOptions)
        };
        Command parserTypes = new("parser-types", "Inspect registered observation parser capabilities.")
        {
            CreateParserTypeListCommand(commands.Services, globalOptions)
        };
        Command connections = new("connections", "Manage adapter connections.")
        {
            CreateConnectionListCommand(commands.Services, globalOptions),
            CreateConnectionGetCommand(commands.Services, globalOptions),
            CreateConnectionHealthCommand(commands.Services, globalOptions),
            CreateConnectionCreateCommand(commands.Services, globalOptions),
            CreateConnectionUpdateCommand(commands.Services, globalOptions),
            CreatePollingScheduleCommand(commands.Services, globalOptions, clear: false),
            CreatePollingScheduleCommand(commands.Services, globalOptions, clear: true),
            CreateConnectionStateCommand(commands.Services, globalOptions, enabled: true),
            CreateConnectionStateCommand(commands.Services, globalOptions, enabled: false),
            CreateCheckpointResetCommand(commands.Services, globalOptions)
        };
        Command runs = new("runs", "Inspect and control adapter runs.")
        {
            CreateRunListCommand(commands.Services, globalOptions),
            CreateRunGetCommand(commands.Services, globalOptions),
            CreateRunEnqueueCommand(commands.Services, globalOptions),
            CreateRunControlCommand(commands.Services, globalOptions, retry: true),
            CreateRunControlCommand(commands.Services, globalOptions, retry: false)
        };
        Command credentials = new("credentials", "Manage adapter ingress credentials.")
        {
            CreateCredentialListCommand(commands.Services, globalOptions),
            CreateCredentialCreateCommand(commands.Services, globalOptions),
            CreateCredentialRevokeCommand(commands.Services, globalOptions)
        };
        Command receipts = new("receipts", "Inspect durable observation receipts.")
        {
            CreateReceiptListCommand(commands.Services, globalOptions),
            CreateReceiptGetCommand(commands.Services, globalOptions),
            CreateReceiptDownloadCommand(commands.Services, globalOptions)
        };
        Command retention = new("retention", "Run sensitive-data retention operations.")
        {
            CreateRawPayloadPurgeCommand(commands.Services, globalOptions),
            CreateSensitiveHistoryRedactionCommand(commands.Services, globalOptions)
        };
        Command reprocessing = new("reprocessing", "Reprocess retained rejected observations.")
        {
            CreateReprocessingListCommand(commands.Services, globalOptions),
            CreateReprocessingGetCommand(commands.Services, globalOptions),
            CreateReprocessingEnqueueCommand(commands.Services, globalOptions),
            CreateReprocessingCancelCommand(commands.Services, globalOptions)
        };
        Command legalHolds = new("legal-holds", "Manage property legal holds.")
        {
            CreateLegalHoldListCommand(commands.Services, globalOptions),
            CreateLegalHoldGetCommand(commands.Services, globalOptions),
            CreateLegalHoldPlaceCommand(commands.Services, globalOptions),
            CreateLegalHoldReleaseCommand(commands.Services, globalOptions)
        };
        Command proposals = new("proposals", "Inspect and decide reservation change proposals.")
        {
            CreateListCommand(commands.Services, globalOptions),
            CreateGetCommand(commands.Services, globalOptions),
            CreateAcceptCommand(commands.Services, globalOptions),
            CreateRejectCommand(commands.Services, globalOptions)
        };
        Command module = new(IngestionModuleMetadata.Name, "Ingestion administration operations.")
        {
            adapterTypes, parserTypes, connections, credentials, runs, receipts, reprocessing, retention, legalHolds,
            proposals
        };
        commands.AddCommand(this.Name, module);
    }

    private static Task<int> ExecuteObjectAsync<T>(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        ParseResult parseResult,
        string operationName,
        AdminPermission permission,
        Func<IServiceProvider, CancellationToken, Task<Result<T>>> execute,
        CancellationToken cancellationToken) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
        parseResult,
        AdminOperation.Create(operationName, permission),
        parseResult.GetValue(globalOptions.TenantOption),
        requireTenant: true,
        async (provider, token) =>
        {
            Result<T> result = await execute(provider, token).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                AdminCliOutput.WriteObject(result.Value, Output(parseResult, globalOptions));
            }

            return result;
        },
        cancellationToken);

    private static Result<T> ParseEnum<T>(string value)
        where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out T parsed) && Enum.IsDefined(parsed) &&
        Convert.ToInt32(parsed, System.Globalization.CultureInfo.InvariantCulture) != 0
            ? Result.Success(parsed)
            : Result.Failure<T>(IngestionApplicationErrors.OperatorValueInvalid);

    private static Result<OptionalEnumValue<T>> ParseOptionalEnum<T>(string? value)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success(new OptionalEnumValue<T>(null));
        }

        Result<T> parsed = ParseEnum<T>(value);
        return parsed.IsSuccess
            ? Result.Success(new OptionalEnumValue<T>(parsed.Value))
            : Result.Failure<OptionalEnumValue<T>>(parsed.Error);
    }

    private static Command CreateConnectionListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<string?> status = new("--status");
        Option<int> page = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSize = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List adapter connections.") { property, status, page, pageSize };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.ConnectionList, IngestionAdminPermissions.Read,
            async (provider, ct) =>
            {
                Result<OptionalEnumValue<AdapterConnectionStatus>> parsed =
                    ParseOptionalEnum<AdapterConnectionStatus>(parse.GetValue(status));
                return parsed.IsFailure
                    ? Result.Failure<AdapterConnectionListResponse>(parsed.Error)
                    : await provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                        new ListAdapterConnectionsQuery(parse.GetRequiredValue(property), parsed.Value.Value,
                            parse.GetValue(page), parse.GetValue(pageSize)), ct).ConfigureAwait(false);
            }, token));
        return command;
    }

    private static Command CreateConnectionGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Command command = new("get", "Get an adapter connection.") { property, connection };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.ConnectionGet, IngestionAdminPermissions.Read,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new GetAdapterConnectionQuery(parse.GetRequiredValue(property), parse.GetRequiredValue(connection)), ct), token));
        return command;
    }

    private static Command CreateConnectionHealthCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Command command = new("health", "Get factual connection operational health.") { property, connection };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.ConnectionHealth,
            IngestionAdminPermissions.Read,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new GetAdapterConnectionHealthQuery(
                    parse.GetRequiredValue(property),
                    parse.GetRequiredValue(connection)),
                ct),
            token));
        return command;
    }

    private static Command CreateAdapterTypeListCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Command command = new("list", "List registered adapter capabilities.");
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.AdapterTypeList,
            IngestionAdminPermissions.Read,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new ListAdapterTypeCapabilitiesQuery(), ct),
            token));
        return command;
    }

    private static Command CreateParserTypeListCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Command command = new("list", "List registered observation parser capabilities.");
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.ParserTypeList,
            IngestionAdminPermissions.Read,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new ListObservationParserCapabilitiesQuery(), ct),
            token));
        return command;
    }

    private static Command CreateConnectionCreateCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<string> adapter = RequiredString("--adapter-type");
        Option<string> mode = RequiredString("--execution-mode");
        Option<string> policy = RequiredString("--conflict-policy");
        Option<string> configuration = RequiredString("--configuration-reference");
        Option<string?> secret = new("--secret-reference");
        Command command = new("create", "Create an adapter connection.")
        {
            property, adapter, mode, policy, configuration, secret
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.ConnectionCreate,
            IngestionAdminPermissions.ConnectionsManage,
            async (provider, ct) =>
            {
                Result<AdapterExecutionMode> parsedMode = ParseEnum<AdapterExecutionMode>(parse.GetRequiredValue(mode));
                Result<AdapterConflictPolicy> parsedPolicy = ParseEnum<AdapterConflictPolicy>(parse.GetRequiredValue(policy));
                if (parsedMode.IsFailure || parsedPolicy.IsFailure)
                {
                    return Result.Failure<AdapterConnectionDto>(
                        parsedMode.IsFailure ? parsedMode.Error : parsedPolicy.Error);
                }

                return await provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new CreateAdapterConnectionCommand(parse.GetRequiredValue(property), parse.GetRequiredValue(adapter),
                        parsedMode.Value, parsedPolicy.Value, parse.GetRequiredValue(configuration), parse.GetValue(secret)), ct)
                    .ConfigureAwait(false);
            }, token));
        return command;
    }

    private static Command CreateConnectionUpdateCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Option<string> mode = RequiredString("--execution-mode");
        Option<string> policy = RequiredString("--conflict-policy");
        Option<string> configuration = RequiredString("--configuration-reference");
        Option<string?> secret = new("--secret-reference");
        Option<bool> clearSecret = new("--clear-secret-reference");
        Option<long> version = RequiredLong("--expected-version");
        Command command = new("update", "Update future-run connection settings.")
        {
            property, connection, mode, policy, configuration, secret, clearSecret, version
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.ConnectionUpdate,
            IngestionAdminPermissions.ConnectionsManage,
            async (provider, ct) =>
            {
                Result<AdapterExecutionMode> parsedMode = ParseEnum<AdapterExecutionMode>(parse.GetRequiredValue(mode));
                Result<AdapterConflictPolicy> parsedPolicy = ParseEnum<AdapterConflictPolicy>(parse.GetRequiredValue(policy));
                if (parsedMode.IsFailure || parsedPolicy.IsFailure)
                {
                    return Result.Failure<AdapterConnectionDto>(
                        parsedMode.IsFailure ? parsedMode.Error : parsedPolicy.Error);
                }

                return await provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new UpdateAdapterConnectionCommand(parse.GetRequiredValue(property), parse.GetRequiredValue(connection),
                        parsedMode.Value, parsedPolicy.Value, parse.GetRequiredValue(configuration),
                        ResolveSecretReferenceUpdateMode(parse.GetValue(secret), parse.GetValue(clearSecret)),
                        parse.GetValue(secret),
                        parse.GetRequiredValue(version)), ct).ConfigureAwait(false);
            }, token));
        return command;
    }

    private static Command CreateConnectionStateCommand(
        IServiceProvider services, AdminCliGlobalOptions globalOptions, bool enabled)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Option<long> version = RequiredLong("--expected-version");
        Command command = new(enabled ? "enable" : "disable", enabled ? "Enable a connection." : "Disable a connection.")
        {
            property, connection, version
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse,
            enabled ? IngestionAdminOperationNames.ConnectionEnable : IngestionAdminOperationNames.ConnectionDisable,
            IngestionAdminPermissions.ConnectionsManage,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                new SetAdapterConnectionEnabledCommand(parse.GetRequiredValue(property), parse.GetRequiredValue(connection),
                    enabled, parse.GetRequiredValue(version)), ct), token));
        return command;
    }

    private static Command CreateCheckpointResetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Option<long> version = RequiredLong("--expected-version");
        Option<bool> yes = new("--yes");
        Command command = new("reset-checkpoint", "Reset a disabled connection checkpoint.")
        {
            property, connection, version, yes
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.ConnectionResetCheckpoint,
            IngestionAdminPermissions.ConnectionsManage,
            (provider, ct) => parse.GetValue(yes)
                ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new ResetAdapterConnectionCheckpointCommand(parse.GetRequiredValue(property),
                        parse.GetRequiredValue(connection), parse.GetRequiredValue(version)), ct)
                : Task.FromResult(Result.Failure<AdapterConnectionDto>(AdminErrors.ConfirmationRequired)), token));
        return command;
    }

    private static Command CreateRunListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid?> connection = new("--connection-id");
        Option<string?> status = new("--status");
        Option<int> page = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSize = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List ingestion runs.") { property, connection, status, page, pageSize };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.RunList, IngestionAdminPermissions.Read,
            async (provider, ct) =>
            {
                Result<OptionalEnumValue<IngestionRunStatus>> parsed =
                    ParseOptionalEnum<IngestionRunStatus>(parse.GetValue(status));
                return parsed.IsFailure
                    ? Result.Failure<IngestionRunListResponse>(parsed.Error)
                    : await provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                        new ListIngestionRunsQuery(parse.GetRequiredValue(property), parse.GetValue(connection), parsed.Value.Value,
                            parse.GetValue(page), parse.GetValue(pageSize)), ct).ConfigureAwait(false);
            }, token));
        return command;
    }

    private static Command CreateRunGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> run = RequiredGuid("--run-id");
        Command command = new("get", "Get an ingestion run.") { property, run };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.RunGet, IngestionAdminPermissions.Read,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new GetIngestionRunQuery(parse.GetRequiredValue(property), parse.GetRequiredValue(run)), ct), token));
        return command;
    }

    private static Command CreateRunEnqueueCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Option<string> actor = RequiredString("--actor");
        Option<DateTimeOffset?> scheduled = new("--scheduled-at-utc");
        Option<int> maxAttempts = new("--max-attempts") { DefaultValueFactory = _ => 3 };
        Option<string?> deduplication = new("--deduplication-key");
        Command command = new("enqueue", "Enqueue an adapter run.")
        {
            property, connection, actor, scheduled, maxAttempts, deduplication
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.RunEnqueue, IngestionAdminPermissions.RunsManage,
            async (provider, ct) =>
            {
                IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                Result<AdapterConnectionDto> found = await dispatcher.QueryAsync(new GetAdapterConnectionQuery(
                    parse.GetRequiredValue(property), parse.GetRequiredValue(connection)), ct).ConfigureAwait(false);
                if (found.IsFailure)
                {
                    return Result.Failure<TaskRunDetails>(found.Error);
                }

                if (found.Value.Status != AdapterConnectionStatus.Enabled || parse.GetValue(maxAttempts) <= 0)
                {
                    return Result.Failure<TaskRunDetails>(
                        found.Value.Status != AdapterConnectionStatus.Enabled
                            ? IngestionApplicationErrors.ConnectionNotEnabled
                            : IngestionApplicationErrors.OperatorValueInvalid);
                }

                if (found.Value.ExecutionMode is
                    AdapterExecutionMode.Push or AdapterExecutionMode.RemotePolling)
                {
                    return Result.Failure<TaskRunDetails>(
                        IngestionApplicationErrors.AdapterExecutionModeNotTaskRunnable);
                }

                Guid connectionId = parse.GetRequiredValue(connection);
                return await dispatcher.SendAsync(new EnqueueTaskRunCommand(
                    null, IngestionModuleMetadata.Name, RunAdapterTaskPayload.TaskName,
                    JsonSerializer.Serialize(new RunAdapterTaskPayload(connectionId)), parse.GetValue(scheduled),
                    IngestionModuleMetadata.AdapterWorkerGroup, parse.GetValue(globalOptions.TenantOption), connectionId,
                    parse.GetRequiredValue(actor), parse.GetValue(maxAttempts), RunAdapterTaskPayload.PayloadVersion,
                    parse.GetValue(deduplication)), ct).ConfigureAwait(false);
            }, token));
        return command;
    }

    private static Command CreateRunControlCommand(
        IServiceProvider services, AdminCliGlobalOptions globalOptions, bool retry)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> run = RequiredGuid("--run-id");
        Option<string> actor = RequiredString("--actor");
        Option<DateTimeOffset?> scheduled = new("--scheduled-at-utc");
        Option<bool> yes = new("--yes");
        Command command = new(retry ? "retry" : "cancel", retry ? "Retry a failed task run." : "Cancel a task run.")
        {
            property, run, actor, yes
        };
        if (retry)
        {
            command.Options.Add(scheduled);
        }

        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse,
            retry ? IngestionAdminOperationNames.RunRetry : IngestionAdminOperationNames.RunCancel,
            IngestionAdminPermissions.RunsManage,
            async (provider, ct) =>
            {
                if (!parse.GetValue(yes))
                {
                    return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
                }

                IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                Result<IngestionRunDto> found = await dispatcher.QueryAsync(new GetIngestionRunQuery(
                    parse.GetRequiredValue(property), parse.GetRequiredValue(run)), ct).ConfigureAwait(false);
                if (found.IsFailure)
                {
                    return Result.Failure<Unit>(found.Error);
                }

                if (!found.Value.TaskRunId.HasValue)
                {
                    return Result.Failure<Unit>(IngestionApplicationErrors.RunNotTaskManaged);
                }

                return retry
                    ? await dispatcher.SendAsync(new RetryTaskRunCommand(found.Value.TaskRunId.Value,
                        parse.GetRequiredValue(actor), parse.GetValue(scheduled)), ct).ConfigureAwait(false)
                    : await dispatcher.SendAsync(new CancelTaskRunCommand(found.Value.TaskRunId.Value,
                        parse.GetRequiredValue(actor)), ct).ConfigureAwait(false);
            }, token));
        return command;
    }

    private static Command CreateRawPayloadPurgeCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<string> requestedBy = RequiredString("--requested-by");
        Option<int> batchSize = new("--batch-size")
        {
            DefaultValueFactory = _ => PurgeExpiredRawPayloadsPayload.DefaultBatchSize
        };
        Option<int> maxBatches = new("--max-batches")
        {
            DefaultValueFactory = _ => PurgeExpiredRawPayloadsPayload.DefaultMaxBatches
        };
        Option<int> staleClaimMinutes = new("--stale-claim-minutes")
        {
            DefaultValueFactory = _ => PurgeExpiredRawPayloadsPayload.DefaultStaleClaimMinutes
        };
        Option<int> maxAttempts = new("--max-attempts") { DefaultValueFactory = _ => 3 };
        Option<string?> deduplication = new("--deduplication-key");
        Option<bool> yes = new("--yes");
        Command command = new("purge-raw-payloads", "Enqueue expired raw-payload purging for one tenant.")
        {
            requestedBy, batchSize, maxBatches, staleClaimMinutes, maxAttempts, deduplication, yes
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.RetentionRawPayloadPurge,
            IngestionAdminPermissions.RetentionManage,
            async (provider, ct) =>
            {
                if (!parse.GetValue(yes))
                {
                    return Result.Failure<TaskRunDetails>(AdminErrors.ConfirmationRequired);
                }

                int selectedBatchSize = parse.GetValue(batchSize);
                int selectedMaxBatches = parse.GetValue(maxBatches);
                int selectedStaleClaimMinutes = parse.GetValue(staleClaimMinutes);
                int selectedMaxAttempts = parse.GetValue(maxAttempts);
                string? tenantId = parse.GetValue(globalOptions.TenantOption);
                if (selectedBatchSize is <= 0 or > PurgeExpiredRawPayloadsPayload.MaximumBatchSize ||
                    selectedMaxBatches is <= 0 or > PurgeExpiredRawPayloadsPayload.MaximumBatches ||
                    selectedStaleClaimMinutes is < PurgeExpiredRawPayloadsPayload.MinimumStaleClaimMinutes or
                        > PurgeExpiredRawPayloadsPayload.MaximumStaleClaimMinutes ||
                    selectedMaxAttempts <= 0 ||
                    string.IsNullOrWhiteSpace(tenantId))
                {
                    return Result.Failure<TaskRunDetails>(IngestionApplicationErrors.RetentionTaskOptionsInvalid);
                }

                PurgeExpiredRawPayloadsPayload payload = new(
                    selectedBatchSize,
                    selectedMaxBatches,
                    selectedStaleClaimMinutes);
                return await provider.GetRequiredService<IRequestDispatcher>().SendAsync(new EnqueueTaskRunCommand(
                    RunId: null,
                    IngestionModuleMetadata.Name,
                    PurgeExpiredRawPayloadsPayload.TaskName,
                    JsonSerializer.Serialize(payload),
                    ScheduledAtUtc: null,
                    IngestionModuleMetadata.MaintenanceWorkerGroup,
                    tenantId,
                    CorrelationId: null,
                    parse.GetRequiredValue(requestedBy),
                    selectedMaxAttempts,
                    PurgeExpiredRawPayloadsPayload.PayloadVersion,
                    parse.GetValue(deduplication)), ct).ConfigureAwait(false);
            },
            token));
        return command;
    }

    private static Command CreateSensitiveHistoryRedactionCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<string> requestedBy = RequiredString("--requested-by");
        Option<int> batchSize = new("--batch-size")
        {
            DefaultValueFactory = _ => RedactExpiredReservationHistoryPayload.DefaultBatchSize
        };
        Option<int> maxBatches = new("--max-batches")
        {
            DefaultValueFactory = _ => RedactExpiredReservationHistoryPayload.DefaultMaxBatches
        };
        Option<int> maxAttempts = new("--max-attempts") { DefaultValueFactory = _ => 3 };
        Option<string?> deduplication = new("--deduplication-key");
        Option<bool> yes = new("--yes");
        Command command = new(
            "redact-reservation-history",
            "Enqueue expired normalized reservation-history redaction for one tenant.")
        {
            requestedBy, batchSize, maxBatches, maxAttempts, deduplication, yes
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.RetentionSensitiveHistoryRedact,
            IngestionAdminPermissions.RetentionManage,
            async (provider, ct) =>
            {
                if (!parse.GetValue(yes))
                {
                    return Result.Failure<TaskRunDetails>(AdminErrors.ConfirmationRequired);
                }

                int selectedBatchSize = parse.GetValue(batchSize);
                int selectedMaxBatches = parse.GetValue(maxBatches);
                int selectedMaxAttempts = parse.GetValue(maxAttempts);
                string? tenantId = parse.GetValue(globalOptions.TenantOption);
                if (selectedBatchSize is <= 0 or > RedactExpiredReservationHistoryPayload.MaximumBatchSize ||
                    selectedMaxBatches is <= 0 or > RedactExpiredReservationHistoryPayload.MaximumBatches ||
                    selectedMaxAttempts <= 0 ||
                    string.IsNullOrWhiteSpace(tenantId))
                {
                    return Result.Failure<TaskRunDetails>(IngestionApplicationErrors.RetentionTaskOptionsInvalid);
                }

                RedactExpiredReservationHistoryPayload payload = new(
                    selectedBatchSize,
                    selectedMaxBatches);
                return await provider.GetRequiredService<IRequestDispatcher>().SendAsync(new EnqueueTaskRunCommand(
                    RunId: null,
                    IngestionModuleMetadata.Name,
                    RedactExpiredReservationHistoryPayload.TaskName,
                    JsonSerializer.Serialize(payload),
                    ScheduledAtUtc: null,
                    IngestionModuleMetadata.MaintenanceWorkerGroup,
                    tenantId,
                    CorrelationId: null,
                    parse.GetRequiredValue(requestedBy),
                    selectedMaxAttempts,
                    RedactExpiredReservationHistoryPayload.PayloadVersion,
                    parse.GetValue(deduplication)), ct).ConfigureAwait(false);
            },
            token));
        return command;
    }

    private static Command CreatePollingScheduleCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        bool clear)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Option<int> interval = new("--interval-seconds");
        Option<int> maxAttempts = new("--max-attempts") { DefaultValueFactory = _ => 3 };
        Option<long> version = new("--expected-version") { Required = true };
        Command command = new(
            clear ? "clear-schedule" : "schedule",
            clear ? "Clear a polling schedule." : "Configure a polling schedule.")
        {
            property,
            connection,
            version
        };
        if (!clear)
        {
            interval.Required = true;
            command.Options.Add(interval);
            command.Options.Add(maxAttempts);
        }

        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            clear
                ? IngestionAdminOperationNames.ConnectionPollingScheduleClear
                : IngestionAdminOperationNames.ConnectionPollingScheduleConfigure,
            IngestionAdminPermissions.ConnectionsManage,
            (provider, ct) => clear
                ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new ClearAdapterConnectionPollingScheduleCommand(
                        parse.GetRequiredValue(property),
                        parse.GetRequiredValue(connection),
                        parse.GetValue(version)), ct)
                : provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new ConfigureAdapterConnectionPollingScheduleCommand(
                        parse.GetRequiredValue(property),
                        parse.GetRequiredValue(connection),
                        parse.GetValue(interval),
                        parse.GetValue(maxAttempts),
                        parse.GetValue(version)), ct),
            token));
        return command;
    }

    private static Command CreateCredentialListCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Option<int> page = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSize = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List adapter ingress credentials.")
        {
            property, connection, page, pageSize
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.CredentialList,
            IngestionAdminPermissions.CredentialsManage,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new ListAdapterIngressCredentialsQuery(
                    parse.GetRequiredValue(property),
                    parse.GetRequiredValue(connection),
                    parse.GetValue(page),
                    parse.GetValue(pageSize)),
                ct),
            token));
        return command;
    }

    private static Command CreateCredentialCreateCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Option<string> label = RequiredString("--label");
        Option<DateTimeOffset?> expires = new("--expires-at-utc");
        Command command = new("create", "Create an adapter ingress credential and print its token once.")
        {
            property, connection, label, expires
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.CredentialCreate,
            IngestionAdminPermissions.CredentialsManage,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                new CreateAdapterIngressCredentialCommand(
                    parse.GetRequiredValue(property),
                    parse.GetRequiredValue(connection),
                    parse.GetRequiredValue(label),
                    parse.GetValue(expires),
                    $"admin-cli:{ResolveActor(parse, globalOptions)}"),
                ct),
            token));
        return command;
    }

    private static Command CreateCredentialRevokeCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> connection = RequiredGuid("--connection-id");
        Option<Guid> credential = RequiredGuid("--credential-id");
        Option<long> version = RequiredLong("--expected-version");
        Option<bool> yes = new("--yes");
        Command command = new("revoke", "Revoke an adapter ingress credential.")
        {
            property, connection, credential, version, yes
        };
        command.SetAction((parse, token) => ExecuteObjectAsync<AdapterIngressCredentialDto>(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.CredentialRevoke,
            IngestionAdminPermissions.CredentialsManage,
            parse.GetValue(yes)
                ? (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new RevokeAdapterIngressCredentialCommand(
                        parse.GetRequiredValue(property),
                        parse.GetRequiredValue(connection),
                        parse.GetRequiredValue(credential),
                        parse.GetRequiredValue(version),
                        $"admin-cli:{ResolveActor(parse, globalOptions)}"),
                    ct)
                : (_, _) => Task.FromResult(
                    Result.Failure<AdapterIngressCredentialDto>(AdminErrors.ConfirmationRequired)),
            token));
        return command;
    }

    private static Command CreateReprocessingListCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid?> sourceReceipt = new("--source-receipt-id");
        Option<string?> status = new("--status");
        Option<int> page = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSize = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List observation reprocessing attempts.")
        {
            property, sourceReceipt, status, page, pageSize
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.ReprocessingList,
            IngestionAdminPermissions.Read,
            async (provider, ct) =>
            {
                Result<OptionalEnumValue<ObservationReprocessingStatus>> parsed =
                    ParseOptionalEnum<ObservationReprocessingStatus>(parse.GetValue(status));
                return parsed.IsFailure
                    ? Result.Failure<ObservationReprocessingAttemptListResponse>(parsed.Error)
                    : await provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                        new ListObservationReprocessingAttemptsQuery(
                            parse.GetRequiredValue(property),
                            parse.GetValue(sourceReceipt),
                            parsed.Value.Value,
                            parse.GetValue(page),
                            parse.GetValue(pageSize)), ct).ConfigureAwait(false);
            },
            token));
        return command;
    }

    private static Command CreateReprocessingGetCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> attempt = RequiredGuid("--attempt-id");
        Command command = new("get", "Get one observation reprocessing attempt.") { property, attempt };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.ReprocessingGet,
            IngestionAdminPermissions.Read,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new GetObservationReprocessingAttemptQuery(
                    parse.GetRequiredValue(property),
                    parse.GetRequiredValue(attempt)), ct),
            token));
        return command;
    }

    private static Command CreateReprocessingEnqueueCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> sourceReceipt = RequiredGuid("--source-receipt-id");
        Option<string> parserType = RequiredString("--parser-type");
        Option<int?> parserVersion = new("--parser-version");
        Option<DateTimeOffset?> scheduled = new("--scheduled-at-utc");
        Option<int> maxAttempts = new("--max-attempts") { DefaultValueFactory = _ => 3 };
        Option<string?> deduplication = new("--deduplication-key");
        Option<bool> yes = new("--yes");
        Command command = new("enqueue", "Enqueue a parser for one retained rejected observation.")
        {
            property, sourceReceipt, parserType, parserVersion, scheduled, maxAttempts, deduplication, yes
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.ReprocessingEnqueue,
            IngestionAdminPermissions.ReprocessingManage,
            async (provider, ct) =>
            {
                if (!parse.GetValue(yes))
                {
                    return Result.Failure<TaskRunDetails>(AdminErrors.ConfirmationRequired);
                }

                int attempts = parse.GetValue(maxAttempts);
                if (attempts is <= 0 or > ReprocessObservationPayload.MaximumAttempts)
                {
                    return Result.Failure<TaskRunDetails>(IngestionApplicationErrors.OperatorValueInvalid);
                }

                IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                string requestedBy = ResolveActor(parse, globalOptions);
                Result<ObservationReprocessingPreparation> prepared = await dispatcher.SendAsync(
                    new PrepareObservationReprocessingCommand(
                        parse.GetRequiredValue(property),
                        parse.GetRequiredValue(sourceReceipt),
                        parse.GetRequiredValue(parserType),
                        parse.GetValue(parserVersion),
                        requestedBy,
                        parse.GetValue(scheduled)), ct).ConfigureAwait(false);
                if (prepared.IsFailure)
                {
                    return Result.Failure<TaskRunDetails>(prepared.Error);
                }

                try
                {
                    ReprocessObservationPayload payload = new(
                        prepared.Value.AttemptId,
                        prepared.Value.ParserType,
                        prepared.Value.ParserVersion,
                        attempts);
                    Result<TaskRunDetails> enqueued = await dispatcher.SendAsync(new EnqueueTaskRunCommand(
                        prepared.Value.TaskRunId,
                        IngestionModuleMetadata.Name,
                        ReprocessObservationPayload.TaskName,
                        JsonSerializer.Serialize(payload),
                        prepared.Value.ScheduledAtUtc,
                        IngestionModuleMetadata.MaintenanceWorkerGroup,
                        parse.GetValue(globalOptions.TenantOption),
                        prepared.Value.SourceReceiptId,
                        requestedBy,
                        attempts,
                        ReprocessObservationPayload.PayloadVersion,
                        parse.GetValue(deduplication) ?? $"reprocess:{prepared.Value.AttemptId:N}"), ct)
                        .ConfigureAwait(false);
                    if (enqueued.IsFailure)
                    {
                        _ = await dispatcher.SendAsync(new FailPreparedObservationReprocessingCommand(
                            prepared.Value.AttemptId,
                            IngestionApplicationErrors.ReprocessingEnqueueFailed.Code), CancellationToken.None)
                            .ConfigureAwait(false);
                    }

                    return enqueued;
                }
                catch
                {
                    _ = await dispatcher.SendAsync(new FailPreparedObservationReprocessingCommand(
                        prepared.Value.AttemptId,
                        IngestionApplicationErrors.ReprocessingEnqueueFailed.Code), CancellationToken.None)
                        .ConfigureAwait(false);
                    throw;
                }
            },
            token));
        return command;
    }

    private static Command CreateReprocessingCancelCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> attempt = RequiredGuid("--attempt-id");
        Option<bool> yes = new("--yes");
        Command command = new("cancel", "Cancel an observation reprocessing attempt.")
        {
            property, attempt, yes
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.ReprocessingCancel,
            IngestionAdminPermissions.ReprocessingManage,
            async (provider, ct) =>
            {
                if (!parse.GetValue(yes))
                {
                    return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
                }

                IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                Result<ObservationReprocessingAttemptDetailsDto> found = await dispatcher.QueryAsync(
                    new GetObservationReprocessingAttemptQuery(
                        parse.GetRequiredValue(property),
                        parse.GetRequiredValue(attempt)), ct).ConfigureAwait(false);
                if (found.IsFailure)
                {
                    return Result.Failure<Unit>(found.Error);
                }

                if (found.Value.Attempt.Status is ObservationReprocessingStatus.Succeeded or
                    ObservationReprocessingStatus.NoMatch or ObservationReprocessingStatus.Failed or
                    ObservationReprocessingStatus.Canceled or ObservationReprocessingStatus.Expired)
                {
                    return Result.Success(Unit.Value);
                }

                Result<TaskRunDetails> task = await dispatcher.QueryAsync(
                    new GetTaskRunQuery(found.Value.Attempt.TaskRunId), ct).ConfigureAwait(false);
                if (task.IsFailure)
                {
                    return Result.Failure<Unit>(task.Error);
                }

                if (task.Value.Summary.Status is TaskRunStatus.Succeeded or TaskRunStatus.Failed or
                    TaskRunStatus.Canceled or TaskRunStatus.TimedOut)
                {
                    return await dispatcher.SendAsync(new CancelObservationReprocessingCommand(
                        parse.GetRequiredValue(attempt)), ct).ConfigureAwait(false);
                }

                Result<Unit> canceled = await dispatcher.SendAsync(new CancelTaskRunCommand(
                    task.Value.Summary.RunId,
                    ResolveActor(parse, globalOptions)), ct).ConfigureAwait(false);
                if (canceled.IsFailure)
                {
                    return canceled;
                }

                return task.Value.Summary.Status is TaskRunStatus.Queued or TaskRunStatus.RetryScheduled
                    ? await dispatcher.SendAsync(new CancelObservationReprocessingCommand(
                        parse.GetRequiredValue(attempt)), ct).ConfigureAwait(false)
                    : Result.Success(Unit.Value);
            },
            token));
        return command;
    }

    private static Command CreateReceiptListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid?> connection = new("--connection-id");
        Option<Guid?> run = new("--run-id");
        Option<string?> status = new("--status");
        Option<int> page = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSize = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List observation receipts.")
        {
            property, connection, run, status, page, pageSize
        };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.ReceiptList, IngestionAdminPermissions.Read,
            async (provider, ct) =>
            {
                Result<OptionalEnumValue<ObservationReceiptStatus>> parsed =
                    ParseOptionalEnum<ObservationReceiptStatus>(parse.GetValue(status));
                return parsed.IsFailure
                    ? Result.Failure<ObservationReceiptListResponse>(parsed.Error)
                    : await provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                        new ListObservationReceiptsQuery(parse.GetRequiredValue(property), parse.GetValue(connection),
                            parse.GetValue(run), parsed.Value.Value, parse.GetValue(page), parse.GetValue(pageSize)), ct)
                        .ConfigureAwait(false);
            }, token));
        return command;
    }

    private static Command CreateReceiptGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> receipt = RequiredGuid("--receipt-id");
        Command command = new("get", "Get an observation receipt.") { property, receipt };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services, globalOptions, parse, IngestionAdminOperationNames.ReceiptGet, IngestionAdminPermissions.Read,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new GetObservationReceiptQuery(parse.GetRequiredValue(property), parse.GetRequiredValue(receipt)), ct), token));
        return command;
    }

    private static Command CreateReceiptDownloadCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> receipt = RequiredGuid("--receipt-id");
        Option<string> output = RequiredString("--output-file");
        Option<bool> overwrite = new("--overwrite");
        Option<bool> yes = new("--yes");
        Command command = new("download", "Download a sensitive raw observation payload.")
        {
            property, receipt, output, overwrite, yes
        };
        command.SetAction((parse, token) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parse,
            AdminOperation.Create(
                IngestionAdminOperationNames.ReceiptRawPayloadDownload,
                IngestionAdminPermissions.RawPayloadsRead),
            parse.GetValue(globalOptions.TenantOption),
            requireTenant: true,
            async (provider, ct) =>
            {
                if (!parse.GetValue(yes))
                {
                    return Result.Failure<RawPayloadDownloadReport>(AdminErrors.ConfirmationRequired);
                }

                Result<ObservationRawPayload> payload = await provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                    new GetObservationRawPayloadQuery(
                        parse.GetRequiredValue(property),
                        parse.GetRequiredValue(receipt)),
                    ct).ConfigureAwait(false);
                if (payload.IsFailure)
                {
                    return Result.Failure<RawPayloadDownloadReport>(payload.Error);
                }

                Result<RawPayloadDownloadReport> written = await WriteRawPayloadAsync(
                    parse.GetRequiredValue(output),
                    parse.GetValue(overwrite),
                    payload.Value,
                    ct).ConfigureAwait(false);
                if (written.IsSuccess)
                {
                    AdminCliOutput.WriteObject(written.Value, Output(parse, globalOptions));
                }

                return written;
            },
            token));
        return command;
    }

    private static async Task<Result<RawPayloadDownloadReport>> WriteRawPayloadAsync(
        string path,
        bool overwrite,
        ObservationRawPayload payload,
        CancellationToken cancellationToken)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result.Failure<RawPayloadDownloadReport>(OutputPathInvalid);
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return Result.Failure<RawPayloadDownloadReport>(OutputPathInvalid);
        }

        if (!overwrite && File.Exists(fullPath))
        {
            return Result.Failure<RawPayloadDownloadReport>(OutputFileExists);
        }

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(temporaryPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            }))
            {
                await stream.WriteAsync(payload.Content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, fullPath, overwrite);
            return Result.Success(new RawPayloadDownloadReport(
                fullPath,
                payload.Content.Length,
                payload.ContentType,
                payload.ContentSha256));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result.Failure<RawPayloadDownloadReport>(OutputPathInvalid);
        }
        catch (IOException) when (!overwrite && File.Exists(fullPath))
        {
            return Result.Failure<RawPayloadDownloadReport>(OutputFileExists);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // The primary operation result is more useful than temporary-file cleanup failure.
            }
            catch (UnauthorizedAccessException)
            {
                // The primary operation result is more useful than temporary-file cleanup failure.
            }
        }
    }

    private static Command CreateLegalHoldListCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<string?> status = new("--status");
        Option<int> page = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSize = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List property legal holds.") { property, status, page, pageSize };
        command.SetAction((parse, token) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parse,
            AdminOperation.Create(
                IngestionAdminOperationNames.LegalHoldList,
                IngestionAdminPermissions.LegalHoldsManage),
            parse.GetValue(globalOptions.TenantOption),
            requireTenant: true,
            async (provider, ct) =>
            {
                Result<OptionalEnumValue<LegalHoldStatus>> parsed =
                    ParseOptionalEnum<LegalHoldStatus>(parse.GetValue(status));
                if (parsed.IsFailure)
                {
                    return Result.Failure<LegalHoldListResponse>(IngestionApplicationErrors.LegalHoldStatusInvalid);
                }

                Result<LegalHoldListResponse> result = await provider.GetRequiredService<IRequestDispatcher>()
                    .QueryAsync(new ListLegalHoldsQuery(
                        parse.GetRequiredValue(property),
                        parsed.Value.Value,
                        parse.GetValue(page),
                        parse.GetValue(pageSize)), ct)
                    .ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    WriteLegalHolds(result.Value.LegalHolds, Output(parse, globalOptions));
                }

                return result;
            },
            token));
        return command;
    }

    private static Command CreateLegalHoldGetCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> hold = RequiredGuid("--hold-id");
        Command command = new("get", "Get a property legal hold.") { property, hold };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.LegalHoldGet,
            IngestionAdminPermissions.LegalHoldsManage,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new GetLegalHoldQuery(
                    parse.GetRequiredValue(property),
                    parse.GetRequiredValue(hold)), ct),
            token));
        return command;
    }

    private static Command CreateLegalHoldPlaceCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<string> reason = RequiredString("--reason");
        Command command = new("place", "Place a property legal hold.") { property, reason };
        command.SetAction((parse, token) => ExecuteObjectAsync(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.LegalHoldPlace,
            IngestionAdminPermissions.LegalHoldsManage,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                new PlaceLegalHoldCommand(
                    parse.GetRequiredValue(property),
                    parse.GetRequiredValue(reason),
                    $"admin-cli:{ResolveActor(parse, globalOptions)}"), ct),
            token));
        return command;
    }

    private static Command CreateLegalHoldReleaseCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredGuid("--property-id");
        Option<Guid> hold = RequiredGuid("--hold-id");
        Option<long> version = RequiredLong("--expected-version");
        Option<string> reason = RequiredString("--reason");
        Option<bool> yes = new("--yes");
        Command command = new("release", "Release a property legal hold.")
        {
            property, hold, version, reason, yes
        };
        command.SetAction((parse, token) => ExecuteObjectAsync<LegalHoldDto>(
            services,
            globalOptions,
            parse,
            IngestionAdminOperationNames.LegalHoldRelease,
            IngestionAdminPermissions.LegalHoldsManage,
            parse.GetValue(yes)
                ? (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new ReleaseLegalHoldCommand(
                        parse.GetRequiredValue(property),
                        parse.GetRequiredValue(hold),
                        parse.GetRequiredValue(version),
                        parse.GetRequiredValue(reason),
                        $"admin-cli:{ResolveActor(parse, globalOptions)}"), ct)
                : (_, _) => Task.FromResult(
                    Result.Failure<LegalHoldDto>(AdminErrors.ConfirmationRequired)),
            token));
        return command;
    }

    private static Command CreateListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = RequiredGuid("--property-id");
        Option<string?> statusOption = new("--status");
        Option<int> pageOption = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSizeOption = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List change proposals.")
        {
            propertyOption, statusOption, pageOption, pageSizeOption
        };
        command.SetAction((parseResult, cancellationToken) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parseResult,
            AdminOperation.Create(IngestionAdminOperationNames.ProposalList, IngestionAdminPermissions.Read),
            parseResult.GetValue(globalOptions.TenantOption),
            requireTenant: true,
            async (provider, token) =>
            {
                string? statusText = parseResult.GetValue(statusOption);
                ChangeProposalStatus? status = null;
                if (!string.IsNullOrWhiteSpace(statusText))
                {
                    if (!Enum.TryParse(statusText, ignoreCase: true, out ChangeProposalStatus parsed)
                        || !Enum.IsDefined(parsed))
                    {
                        return Result.Failure<ChangeProposalListResponse>(IngestionApplicationErrors.ProposalStatusInvalid);
                    }

                    status = parsed;
                }

                Result<ChangeProposalListResponse> result = await provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                    new ListChangeProposalsQuery(
                        parseResult.GetRequiredValue(propertyOption),
                        status,
                        parseResult.GetValue(pageOption),
                        parseResult.GetValue(pageSizeOption)),
                    token).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    WriteProposals(result.Value.Proposals, Output(parseResult, globalOptions));
                }

                return result;
            },
            cancellationToken));
        return command;
    }

    private static Command CreateGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = RequiredGuid("--property-id");
        Option<Guid> proposalOption = RequiredGuid("--proposal-id");
        Command command = new("get", "Get a change proposal.") { propertyOption, proposalOption };
        command.SetAction((parseResult, cancellationToken) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parseResult,
            AdminOperation.Create(IngestionAdminOperationNames.ProposalGet, IngestionAdminPermissions.Read),
            parseResult.GetValue(globalOptions.TenantOption),
            requireTenant: true,
            async (provider, token) =>
            {
                Result<ChangeProposalDto> result = await provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                    new GetChangeProposalQuery(
                        parseResult.GetRequiredValue(propertyOption),
                        parseResult.GetRequiredValue(proposalOption)),
                    token).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    AdminCliOutput.WriteObject(result.Value, Output(parseResult, globalOptions));
                }

                return result;
            },
            cancellationToken));
        return command;
    }

    private static Command CreateAcceptCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = RequiredGuid("--property-id");
        Option<Guid> proposalOption = RequiredGuid("--proposal-id");
        Option<string> actorOption = RequiredString("--actor");
        Option<long> proposalVersionOption = RequiredLong("--expected-proposal-version");
        Option<long> detailsRevisionOption = RequiredLong("--expected-details-revision");
        Option<bool> yesOption = new("--yes");
        Command command = new("accept", "Accept and dispatch a change proposal.")
        {
            propertyOption, proposalOption, actorOption, proposalVersionOption, detailsRevisionOption, yesOption
        };
        command.SetAction((parseResult, cancellationToken) => ExecuteDecisionAsync(
            services,
            globalOptions,
            parseResult,
            IngestionAdminOperationNames.ProposalAccept,
            parseResult.GetValue(yesOption)
                ? (provider, token) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new AcceptChangeProposalCommand(
                        parseResult.GetRequiredValue(propertyOption),
                        parseResult.GetRequiredValue(proposalOption),
                        parseResult.GetRequiredValue(actorOption),
                        parseResult.GetRequiredValue(proposalVersionOption),
                        parseResult.GetRequiredValue(detailsRevisionOption)),
                    token)
                : (_, _) => Task.FromResult(Result.Failure<ChangeProposalDecisionResult>(AdminErrors.ConfirmationRequired)),
            cancellationToken));
        return command;
    }

    private static Command CreateRejectCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = RequiredGuid("--property-id");
        Option<Guid> proposalOption = RequiredGuid("--proposal-id");
        Option<string> actorOption = RequiredString("--actor");
        Option<string> reasonOption = RequiredString("--reason");
        Option<long> proposalVersionOption = RequiredLong("--expected-proposal-version");
        Option<bool> yesOption = new("--yes");
        Command command = new("reject", "Reject a change proposal.")
        {
            propertyOption, proposalOption, actorOption, reasonOption, proposalVersionOption, yesOption
        };
        command.SetAction((parseResult, cancellationToken) => ExecuteDecisionAsync(
            services,
            globalOptions,
            parseResult,
            IngestionAdminOperationNames.ProposalReject,
            parseResult.GetValue(yesOption)
                ? (provider, token) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new RejectChangeProposalCommand(
                        parseResult.GetRequiredValue(propertyOption),
                        parseResult.GetRequiredValue(proposalOption),
                        parseResult.GetRequiredValue(actorOption),
                        parseResult.GetRequiredValue(reasonOption),
                        parseResult.GetRequiredValue(proposalVersionOption)),
                    token)
                : (_, _) => Task.FromResult(Result.Failure<ChangeProposalDecisionResult>(AdminErrors.ConfirmationRequired)),
            cancellationToken));
        return command;
    }

    private static Task<int> ExecuteDecisionAsync(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        ParseResult parseResult,
        string operationName,
        Func<IServiceProvider, CancellationToken, Task<Result<ChangeProposalDecisionResult>>> execute,
        CancellationToken cancellationToken) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parseResult,
            AdminOperation.Create(operationName, IngestionAdminPermissions.ProposalsDecide),
            parseResult.GetValue(globalOptions.TenantOption),
            requireTenant: true,
            async (provider, token) =>
            {
                Result<ChangeProposalDecisionResult> result = await execute(provider, token).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    AdminCliOutput.WriteObject(result.Value, Output(parseResult, globalOptions));
                }

                return result;
            },
            cancellationToken);

    private static void WriteProposals(IReadOnlyCollection<ChangeProposalSummaryDto> proposals, string output) =>
        AdminCliOutput.WriteRows(
            proposals,
            output,
            [
                ("Proposal", proposal => proposal.ProposalId.ToString("D")),
                ("Reservation", proposal => proposal.ReservationId.ToString("D")),
                ("Status", proposal => proposal.Status.ToString()),
                ("Reason", proposal => proposal.ReasonCode),
                ("Sensitive data", proposal => proposal.SensitiveHistoryStatus.ToString()),
                ("Base rev", proposal => proposal.BaseReservationDetailsRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("Version", proposal => proposal.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("Created UTC", proposal => proposal.CreatedAtUtc.ToString("O"))
            ]);

    private static void WriteLegalHolds(IReadOnlyCollection<LegalHoldDto> legalHolds, string output) =>
        AdminCliOutput.WriteRows(
            legalHolds,
            output,
            [
                ("Hold", legalHold => legalHold.HoldId.ToString("D")),
                ("Property", legalHold => legalHold.PropertyId.ToString("D")),
                ("Status", legalHold => legalHold.Status.ToString()),
                ("Reason", legalHold => legalHold.Reason),
                ("Placed by", legalHold => legalHold.PlacedBy),
                ("Placed UTC", legalHold => legalHold.PlacedAtUtc.ToString("O")),
                ("Version", legalHold => legalHold.Version.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ]);

    private static string Output(ParseResult parseResult, AdminCliGlobalOptions globalOptions) =>
        parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table;

    private static string ResolveActor(ParseResult parseResult, AdminCliGlobalOptions globalOptions) =>
        string.IsNullOrWhiteSpace(parseResult.GetValue(globalOptions.ActorOption))
            ? $"{Environment.UserDomainName}\\{Environment.UserName}"
            : parseResult.GetValue(globalOptions.ActorOption)!.Trim();

    private static SecretReferenceUpdateMode ResolveSecretReferenceUpdateMode(
        string? secretReference,
        bool clearSecretReference) => (secretReference, clearSecretReference) switch
        {
            (not null, true) => SecretReferenceUpdateMode.Unknown,
            (not null, false) => SecretReferenceUpdateMode.Replace,
            (null, true) => SecretReferenceUpdateMode.Clear,
            _ => SecretReferenceUpdateMode.Keep
        };

    private static Option<Guid> RequiredGuid(string name) => new(name) { Required = true };
    private static Option<long> RequiredLong(string name) => new(name) { Required = true };
    private static Option<string> RequiredString(string name) => new(name) { Required = true };

    private static readonly Error OutputPathInvalid = new(
        "Ingestion.RawPayloadOutputPathInvalid",
        "The raw-payload output path is invalid or its directory is unavailable.");

    private static readonly Error OutputFileExists = new(
        "Ingestion.RawPayloadOutputFileExists",
        "The raw-payload output file already exists; use --overwrite with --yes to replace it.");

    private sealed record RawPayloadDownloadReport(
        string OutputFile,
        int ContentLength,
        string SourceContentType,
        string ContentSha256);

    private readonly record struct OptionalEnumValue<T>(T? Value)
        where T : struct, Enum;
}
