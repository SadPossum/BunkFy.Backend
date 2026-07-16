namespace BunkFy.Modules.Reservations.AdminCli;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using BunkFy.Modules.Reservations.Admin.Contracts;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Microsoft.Extensions.DependencyInjection;

internal static class ReservationInventoryAdminCliCommand
{
    public static Command Create(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> property = new("--property-id") { Required = true };
        Option<Guid> reservation = new("--reservation-id") { Required = true };
        Option<Guid> amendment = new("--amendment-request-id") { Required = true };
        Option<string> units = new("--unit-ids") { Required = true };
        Option<long> detailsRevision = new("--expected-details-revision") { Required = true };
        Option<bool> yes = new("--yes");
        Command command = new("reassign-inventory", "Atomically move a confirmed reservation to different inventory.")
        {
            property,
            reservation,
            amendment,
            units,
            detailsRevision,
            yes
        };
        command.SetAction((parse, cancellationToken) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parse,
            AdminOperation.Create(
                ReservationsAdminOperationNames.ReassignInventory,
                ReservationsAdminPermissions.Manage),
            parse.GetValue(globalOptions.TenantOption),
            requireTenant: true,
            async (provider, token) =>
            {
                if (!parse.GetValue(yes))
                {
                    return Result.Failure<ReservationDto>(AdminErrors.ConfirmationRequired);
                }

                if (!TryParseUnitIds(parse.GetRequiredValue(units), out Guid[] unitIds))
                {
                    return Result.Failure<ReservationDto>(ReservationsApplicationErrors.RequestedUnitsInvalid);
                }

                Result<ReservationDto> result = await provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new ReassignReservationInventoryCommand(
                        parse.GetValue(property),
                        parse.GetValue(reservation),
                        parse.GetValue(amendment),
                        unitIds,
                        parse.GetValue(detailsRevision),
                        ResolveActor(parse, globalOptions)),
                    token).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    AdminCliOutput.WriteRows(
                        [result.Value],
                        parse.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table,
                        [
                            ("ReservationId", item => item.ReservationId.ToString()),
                            ("Status", item => item.Status.ToString()),
                            ("PendingAmendment", item => item.PendingAllocationAmendmentId?.ToString() ?? string.Empty),
                            ("Units", item => string.Join(',', item.InventoryUnitIds)),
                            ("DetailsRevision", item => item.DetailsRevision.ToString(CultureInfo.InvariantCulture))
                        ]);
                }

                return result;
            },
            cancellationToken));
        return command;
    }

    private static bool TryParseUnitIds(string value, out Guid[] unitIds)
    {
        string[] values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        unitIds = values.Select(item => Guid.TryParse(item, out Guid id) ? id : Guid.Empty).ToArray();
        return unitIds.Length is > 0 and <= 100 &&
               unitIds.All(id => id != Guid.Empty) &&
               unitIds.Distinct().Count() == unitIds.Length;
    }

    private static string ResolveActor(ParseResult parse, AdminCliGlobalOptions globalOptions) =>
        string.IsNullOrWhiteSpace(parse.GetValue(globalOptions.ActorOption))
            ? $"{Environment.UserDomainName}\\{Environment.UserName}"
            : parse.GetValue(globalOptions.ActorOption)!.Trim();
}
