namespace BunkFy.Modules.Retention.AdminCli;

using System.CommandLine;
using System.CommandLine.Parsing;
using BunkFy.Modules.Retention.Admin.Contracts;
using BunkFy.Modules.Retention.Application;
using BunkFy.Modules.Retention.Application.Errors;
using BunkFy.Modules.Retention.Application.Queries;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Persistence;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Modules.TaskRuntime.Application.Commands;
using Gma.Modules.TaskRuntime.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class RetentionAdminCliModule : IAdminCliModule
{
    public string Name => RetentionModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(
            RetentionProfiles.Default,
            "BunkFy.Modules.Retention.AdminCli");
        builder.Services.AddRetentionApplication();
        builder.AddRetentionPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions =
            commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command module = new(
            RetentionModuleMetadata.Name,
            "Inspect and control automatic retention.")
        {
            CreateScheduleListCommand(commands.Services, globalOptions),
            CreateRunRetryCommand(commands.Services, globalOptions)
        };
        commands.AddCommand(this.Name, module);
    }

    private static Command CreateScheduleListCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<int> page = new("--page")
        {
            DefaultValueFactory = _ => PageRequest.DefaultPage
        };
        Option<int> pageSize = new("--page-size")
        {
            DefaultValueFactory = _ => PageRequest.DefaultPageSize
        };
        Command command = new("list", "List retention schedule health.")
        {
            page,
            pageSize
        };
        command.SetAction((parse, token) =>
            services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
                parse,
                AdminOperation.Create(
                    RetentionAdminOperationNames.ScheduleList,
                    RetentionAdminPermissions.Read),
                parse.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, cancellationToken) =>
                {
                    Result<RetentionScheduleHealthListResponse> result =
                        await provider.GetRequiredService<IRequestDispatcher>()
                            .QueryAsync(
                                new ListRetentionScheduleHealthQuery(
                                    parse.GetValue(page),
                                    parse.GetValue(pageSize)),
                                cancellationToken)
                            .ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        WriteSchedules(
                            result.Value.Items,
                            Output(parse, globalOptions));
                    }

                    return result;
                },
                token));
        return command;
    }

    private static Command CreateRunRetryCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> run = new("--run-id") { Required = true };
        Option<DateTimeOffset?> scheduledAt = new("--scheduled-at-utc");
        Option<bool> yes = new("--yes");
        Command command = new(
            "retry",
            "Retry one failed Retention task run.")
        {
            run,
            scheduledAt,
            yes
        };
        command.SetAction((parse, token) =>
            services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
                parse,
                AdminOperation.Create(
                    RetentionAdminOperationNames.RunRetry,
                    RetentionAdminPermissions.Retry),
                parse.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, cancellationToken) =>
                {
                    Result<RetentionRunRetryReceiptDto> result =
                        parse.GetValue(yes)
                            ? await RetryAsync(
                                parse.GetRequiredValue(run),
                                parse.GetValue(scheduledAt),
                                parse.GetValue(globalOptions.TenantOption),
                                ResolveActor(parse, globalOptions),
                                provider.GetRequiredService<IRequestDispatcher>(),
                                cancellationToken).ConfigureAwait(false)
                            : Result.Failure<RetentionRunRetryReceiptDto>(
                                AdminErrors.ConfirmationRequired);
                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteObject(
                            result.Value,
                            Output(parse, globalOptions));
                    }

                    return result;
                },
                token));
        return command;
    }

    private static async Task<Result<RetentionRunRetryReceiptDto>> RetryAsync(
        Guid runId,
        DateTimeOffset? scheduledAtUtc,
        string? tenantId,
        string actor,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        Result<TaskRunDetails> loaded = await dispatcher.QueryAsync(
            new GetTaskRunQuery(runId),
            cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure<RetentionRunRetryReceiptDto>(loaded.Error);
        }

        TaskRunSummary run = loaded.Value.Summary;
        if (string.IsNullOrWhiteSpace(tenantId) ||
            !string.Equals(run.ScopeId, tenantId, StringComparison.Ordinal) ||
            !string.Equals(
                run.ModuleName,
                RetentionModuleMetadata.Name,
                StringComparison.Ordinal) ||
            !string.Equals(
                run.TaskName,
                ExecuteRetentionSchedulePayload.TaskName,
                StringComparison.Ordinal))
        {
            return Result.Failure<RetentionRunRetryReceiptDto>(
                RetentionApplicationErrors.TaskRunUnavailable);
        }

        Result<Unit> retried = await dispatcher.SendAsync(
            new RetryTaskRunCommand(
                runId,
                $"admin-cli:{actor}",
                scheduledAtUtc),
            cancellationToken).ConfigureAwait(false);
        return retried.IsFailure
            ? Result.Failure<RetentionRunRetryReceiptDto>(retried.Error)
            : Result.Success(new RetentionRunRetryReceiptDto(
                runId,
                scheduledAtUtc));
    }

    private static void WriteSchedules(
        IReadOnlyCollection<RetentionScheduleHealthDto> schedules,
        string output) =>
        AdminCliOutput.WriteRows(
            schedules,
            output,
            [
                ("Owner", schedule => schedule.OwnerKey),
                ("Data class", schedule => schedule.DataClassKey),
                ("Target", schedule =>
                    schedule.PropertyId?.ToString("D") ?? "tenant"),
                ("Status", schedule => schedule.Status.ToString()),
                ("Last run", schedule =>
                    schedule.LastRunId?.ToString("D") ?? string.Empty),
                ("Next due UTC", schedule => schedule.NextDueAtUtc.ToString("O")),
                ("Overdue", schedule => schedule.Overdue ? "yes" : "no"),
                ("Failures", schedule =>
                    schedule.ConsecutiveFailures.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                ("Outcome", schedule => schedule.OutcomeCode ?? string.Empty)
            ]);

    private static string Output(
        ParseResult parse,
        AdminCliGlobalOptions globalOptions) =>
        parse.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table;

    private static string ResolveActor(
        ParseResult parse,
        AdminCliGlobalOptions globalOptions) =>
        string.IsNullOrWhiteSpace(parse.GetValue(globalOptions.ActorOption))
            ? $"{Environment.UserDomainName}\\{Environment.UserName}"
            : parse.GetValue(globalOptions.ActorOption)!.Trim();
}
