namespace BunkFy.Modules.Inventory.AdminCli;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using BunkFy.Modules.Inventory.Admin.Contracts;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Queries;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Microsoft.Extensions.DependencyInjection;

internal static class InventoryBedRetirementAdminCliCommandGroup
{
    public static Command Create(IServiceProvider services, AdminCliGlobalOptions globalOptions) =>
        new("bed-retirements", "Safely drain and retire beds.")
        {
            CreateRequestCommand(services, globalOptions),
            CreateGetCommand(services, globalOptions),
            CreateRetryCommand(services, globalOptions)
        };

    private static Command CreateRequestCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredId("--property-id");
        Option<Guid> room = RequiredId("--room-id");
        Option<Guid> bed = RequiredId("--bed-id");
        Option<string> reason = new("--reason") { Required = true };
        Command command = new("request", "Drain a bed and retire it when active claims are clear.")
        {
            property,
            room,
            bed,
            reason
        };
        command.SetAction((parse, cancellationToken) => ExecuteAsync(
            services,
            globalOptions,
            parse,
            InventoryAdminOperationNames.BedRetirementsRequest,
            (dispatcher, token) => dispatcher.SendAsync(
                new RequestBedRetirementCommand(
                    parse.GetValue(property),
                    parse.GetValue(room),
                    parse.GetValue(bed),
                    parse.GetRequiredValue(reason),
                    ResolveActor(parse, globalOptions)),
                token),
            cancellationToken));
        return command;
    }

    private static Command CreateGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredId("--property-id");
        Option<Guid> change = RequiredId("--topology-change-id");
        Command command = new("get", "Get a bed-retirement process.") { property, change };
        command.SetAction((parse, cancellationToken) => ExecuteAsync(
            services,
            globalOptions,
            parse,
            InventoryAdminOperationNames.BedRetirementsGet,
            (dispatcher, token) => dispatcher.QueryAsync(
                new GetBedRetirementQuery(parse.GetValue(property), parse.GetValue(change)),
                token),
            cancellationToken));
        return command;
    }

    private static Command CreateRetryCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = RequiredId("--property-id");
        Option<Guid> change = RequiredId("--topology-change-id");
        Command command = new("retry", "Retry a rejected bed-retirement finalization.") { property, change };
        command.SetAction((parse, cancellationToken) => ExecuteAsync(
            services,
            globalOptions,
            parse,
            InventoryAdminOperationNames.BedRetirementsRetry,
            (dispatcher, token) => dispatcher.SendAsync(
                new RetryBedRetirementCommand(parse.GetValue(property), parse.GetValue(change)),
                token),
            cancellationToken));
        return command;
    }

    private static Task<int> ExecuteAsync(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        ParseResult parse,
        string operationName,
        Func<IRequestDispatcher, CancellationToken, Task<Result<BedRetirementDto>>> execute,
        CancellationToken cancellationToken) =>
        services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parse,
            AdminOperation.Create(operationName, InventoryAdminPermissions.Configure),
            parse.GetValue(globalOptions.TenantOption),
            requireTenant: true,
            async (provider, token) =>
            {
                Result<BedRetirementDto> result = await execute(
                    provider.GetRequiredService<IRequestDispatcher>(),
                    token).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    Write(result.Value, parse.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                }

                return result;
            },
            cancellationToken);

    private static void Write(BedRetirementDto process, string output) =>
        AdminCliOutput.WriteRows(
            [process],
            output,
            [
                ("TopologyChangeId", item => item.TopologyChangeId.ToString()),
                ("BedId", item => item.BedId.ToString()),
                ("Status", item => item.Status.ToString()),
                ("Allocations", item => item.ActiveAllocationCount.ToString(CultureInfo.InvariantCulture)),
                ("Blocks", item => item.ActiveManualBlockCount.ToString(CultureInfo.InvariantCulture)),
                ("Version", item => item.Version.ToString(CultureInfo.InvariantCulture)),
                ("Reason", item => item.Reason)
            ]);

    private static Option<Guid> RequiredId(string name) => new(name) { Required = true };

    private static string ResolveActor(ParseResult parse, AdminCliGlobalOptions globalOptions) =>
        string.IsNullOrWhiteSpace(parse.GetValue(globalOptions.ActorOption))
            ? $"{Environment.UserDomainName}\\{Environment.UserName}"
            : parse.GetValue(globalOptions.ActorOption)!.Trim();
}
