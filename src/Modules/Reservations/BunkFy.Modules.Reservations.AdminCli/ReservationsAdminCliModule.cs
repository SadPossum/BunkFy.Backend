namespace BunkFy.Modules.Reservations.AdminCli;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BunkFy.Modules.Reservations.Admin.Contracts;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Persistence;

public sealed class ReservationsAdminCliModule : IAdminCliModule
{
    public string Name => ReservationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(ReservationsProfiles.Default, "BunkFy.Modules.Reservations.AdminCli");
        builder.Services.AddReservationsApplication();
        builder.AddReservationsPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command module = new(ReservationsModuleMetadata.Name, "Reservation administration operations.")
        {
            CreateListCommand(commands.Services, globalOptions),
            CreateGetCommand(commands.Services, globalOptions),
            CreateCreateCommand(commands.Services, globalOptions),
            CreateCancelCommand(commands.Services, globalOptions),
            CreateLinkGuestCommand(commands.Services, globalOptions),
            ReservationInventoryAdminCliCommand.Create(commands.Services, globalOptions),
            CreateStayLifecycleCommand(
                commands.Services,
                globalOptions,
                "check-in",
                "Check in a confirmed reservation.",
                ReservationsAdminOperationNames.CheckIn,
                ReservationsAdminPermissions.CheckIn,
                static (propertyId, reservationId, businessDate, expectedVersion, actorId) =>
                    new CheckInReservationCommand(propertyId, reservationId, businessDate, expectedVersion, actorId)),
            CreateStayLifecycleCommand(
                commands.Services,
                globalOptions,
                "no-show",
                "Mark a confirmed reservation as a no-show and release its Inventory allocation.",
                ReservationsAdminOperationNames.NoShow,
                ReservationsAdminPermissions.NoShow,
                static (propertyId, reservationId, businessDate, expectedVersion, actorId) =>
                    new MarkReservationNoShowCommand(propertyId, reservationId, businessDate, expectedVersion, actorId)),
            CreateStayLifecycleCommand(
                commands.Services,
                globalOptions,
                "check-out",
                "Check out a checked-in reservation and release its Inventory allocation.",
                ReservationsAdminOperationNames.CheckOut,
                ReservationsAdminPermissions.CheckOut,
                static (propertyId, reservationId, businessDate, expectedVersion, actorId) =>
                    new CheckOutReservationCommand(propertyId, reservationId, businessDate, expectedVersion, actorId))
        };
        commands.AddCommand(this.Name, module);
    }

    private static Command CreateListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = PropertyOption();
        Option<string?> statusOption = new("--status");
        Option<int> pageOption = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSizeOption = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List reservations.")
        {
            propertyOption,
            statusOption,
            pageOption,
            pageSizeOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(ReservationsAdminOperationNames.List, ReservationsAdminPermissions.Read),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    string? statusText = parseResult.GetValue(statusOption);
                    ReservationStatus? status = string.IsNullOrWhiteSpace(statusText)
                        ? null
                        : Enum.TryParse(statusText, ignoreCase: true, out ReservationStatus parsed) ? parsed : ReservationStatus.Unknown;
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<ReservationListResponse> result = await dispatcher.QueryAsync(
                        new ListReservationsQuery(
                            parseResult.GetRequiredValue(propertyOption),
                            status.HasValue ? [status.Value] : null,
                            null,
                            ReservationListOrder.CreatedDescending,
                            parseResult.GetValue(pageOption),
                            parseResult.GetValue(pageSizeOption)),
                        token).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        WriteReservations(result.Value.Reservations, parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                    }

                    return result;
                },
                cancellationToken);
        });
        return command;
    }

    private static Command CreateGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = PropertyOption();
        Option<Guid> reservationOption = ReservationOption();
        Command command = new("get", "Get a reservation.") { propertyOption, reservationOption };
        command.SetAction((parseResult, cancellationToken) => ExecuteReservationAsync(
            services,
            globalOptions,
            parseResult,
            ReservationsAdminOperationNames.Get,
            ReservationsAdminPermissions.Read,
            provider => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                new GetReservationQuery(
                    parseResult.GetRequiredValue(propertyOption),
                    parseResult.GetRequiredValue(reservationOption)),
                cancellationToken),
            cancellationToken));
        return command;
    }

    private static Command CreateCreateCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = PropertyOption();
        Option<string> arrivalOption = RequiredString("--arrival");
        Option<string> departureOption = RequiredString("--departure");
        Option<string?> expectedArrivalTimeOption = new("--expected-arrival-time");
        Option<string?> expectedDepartureTimeOption = new("--expected-departure-time");
        Option<string> unitIdsOption = RequiredString("--unit-ids");
        Option<string> guestNameOption = RequiredString("--guest-name");
        Option<int> guestCountOption = new("--guest-count") { DefaultValueFactory = _ => 1 };
        Option<string?> emailOption = new("--email");
        Option<string?> phoneOption = new("--phone");
        Option<string?> sourceSystemOption = new("--source-system");
        Option<string?> sourceReferenceOption = new("--source-reference");
        Option<string?> notesOption = new("--notes");
        Command command = new("create", "Create a reservation and request Inventory allocation.")
        {
            propertyOption,
            arrivalOption,
            departureOption,
            expectedArrivalTimeOption,
            expectedDepartureTimeOption,
            unitIdsOption,
            guestNameOption,
            guestCountOption,
            emailOption,
            phoneOption,
            sourceSystemOption,
            sourceReferenceOption,
            notesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(ReservationsAdminOperationNames.Create, ReservationsAdminPermissions.Create),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    if (!TryParseDate(parseResult.GetRequiredValue(arrivalOption), out DateOnly arrival) ||
                        !TryParseDate(parseResult.GetRequiredValue(departureOption), out DateOnly departure) ||
                        arrival >= departure)
                    {
                        return Result.Failure<ReservationDto>(ReservationsApplicationErrors.StayRangeInvalid);
                    }

                    if (!TryParseOptionalTime(parseResult.GetValue(expectedArrivalTimeOption), out TimeOnly? expectedArrivalTime) ||
                        !TryParseOptionalTime(parseResult.GetValue(expectedDepartureTimeOption), out TimeOnly? expectedDepartureTime))
                    {
                        return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ExpectedStayTimeInvalid);
                    }

                    if (!TryParseUnitIds(parseResult.GetRequiredValue(unitIdsOption), out Guid[] unitIds))
                    {
                        return Result.Failure<ReservationDto>(ReservationsApplicationErrors.RequestedUnitsInvalid);
                    }

                    string? sourceSystem = parseResult.GetValue(sourceSystemOption);
                    string? sourceReference = parseResult.GetValue(sourceReferenceOption);
                    ReservationSourceKind sourceKind = string.IsNullOrWhiteSpace(sourceSystem) && string.IsNullOrWhiteSpace(sourceReference)
                        ? ReservationSourceKind.Direct
                        : ReservationSourceKind.External;
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<ReservationDto> result = await dispatcher.SendAsync(
                        new CreateReservationCommand(
                            parseResult.GetRequiredValue(propertyOption),
                            arrival,
                            departure,
                            unitIds,
                            parseResult.GetRequiredValue(guestNameOption),
                            parseResult.GetValue(emailOption),
                            parseResult.GetValue(phoneOption),
                            parseResult.GetValue(guestCountOption),
                            sourceKind,
                            sourceSystem,
                            sourceReference,
                            parseResult.GetValue(notesOption),
                            expectedArrivalTime,
                            expectedDepartureTime),
                        token).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        WriteReservations([result.Value], parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                    }

                    return result;
                },
                cancellationToken);
        });
        return command;
    }

    private static Command CreateCancelCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = PropertyOption();
        Option<Guid> reservationOption = ReservationOption();
        Option<long> versionOption = new("--expected-version") { Required = true };
        Option<bool> yesOption = new("--yes");
        Command command = new("cancel", "Cancel a reservation and release its Inventory allocation.")
        {
            propertyOption,
            reservationOption,
            versionOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) => ExecuteReservationAsync(
            services,
            globalOptions,
            parseResult,
            ReservationsAdminOperationNames.Cancel,
            ReservationsAdminPermissions.Cancel,
            provider => parseResult.GetValue(yesOption)
                ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new CancelReservationCommand(
                        parseResult.GetRequiredValue(propertyOption),
                        parseResult.GetRequiredValue(reservationOption),
                        parseResult.GetRequiredValue(versionOption)),
                    cancellationToken)
                : Task.FromResult(Result.Failure<ReservationDto>(AdminErrors.ConfirmationRequired)),
            cancellationToken));
        return command;
    }

    private static Command CreateStayLifecycleCommand(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        string name,
        string description,
        string operationName,
        AdminPermission permission,
        Func<Guid, Guid, DateOnly, long, string, ICommand<ReservationDto>> createCommand)
    {
        Option<Guid> propertyOption = PropertyOption();
        Option<Guid> reservationOption = ReservationOption();
        Option<string> businessDateOption = RequiredString("--business-date");
        Option<long> versionOption = new("--expected-version") { Required = true };
        Option<bool> yesOption = new("--yes");
        Command command = new(name, description)
        {
            propertyOption,
            reservationOption,
            businessDateOption,
            versionOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) => ExecuteReservationAsync(
            services,
            globalOptions,
            parseResult,
            operationName,
            permission,
            provider =>
            {
                if (!parseResult.GetValue(yesOption))
                {
                    return Task.FromResult(Result.Failure<ReservationDto>(AdminErrors.ConfirmationRequired));
                }

                if (!TryParseDate(parseResult.GetRequiredValue(businessDateOption), out DateOnly businessDate))
                {
                    return Task.FromResult(Result.Failure<ReservationDto>(
                        ReservationsApplicationErrors.StayBusinessDateInvalid));
                }

                return provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    createCommand(
                        parseResult.GetRequiredValue(propertyOption),
                        parseResult.GetRequiredValue(reservationOption),
                        businessDate,
                        parseResult.GetRequiredValue(versionOption),
                        ResolveActor(parseResult, globalOptions)),
                    cancellationToken);
            },
            cancellationToken));
        return command;
    }

    private static Command CreateLinkGuestCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyOption = PropertyOption();
        Option<Guid> reservationOption = ReservationOption();
        Option<Guid> guestOption = new("--guest-id") { Required = true };
        Option<string> roleOption = new("--role") { DefaultValueFactory = _ => nameof(ReservationGuestRoleKind.Primary) };
        Option<bool> replaceOption = new("--replace-existing-role");
        Option<long> versionOption = new("--expected-version") { Required = true };
        Command command = new("link-guest", "Link a canonical guest to a reservation role.")
        {
            propertyOption,
            reservationOption,
            guestOption,
            roleOption,
            replaceOption,
            versionOption
        };
        command.SetAction((parseResult, cancellationToken) => ExecuteReservationAsync(
            services,
            globalOptions,
            parseResult,
            ReservationsAdminOperationNames.LinkGuest,
            ReservationsAdminPermissions.ManageGuests,
            provider =>
            {
                ReservationGuestRoleKind role = Enum.TryParse(
                    parseResult.GetRequiredValue(roleOption),
                    ignoreCase: true,
                    out ReservationGuestRoleKind parsedRole)
                    ? parsedRole
                    : ReservationGuestRoleKind.Unknown;
                return provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new LinkReservationGuestCommand(
                        parseResult.GetRequiredValue(propertyOption),
                        parseResult.GetRequiredValue(reservationOption),
                        parseResult.GetRequiredValue(guestOption),
                        role,
                        parseResult.GetValue(replaceOption),
                        parseResult.GetRequiredValue(versionOption),
                        ResolveActor(parseResult, globalOptions)),
                    cancellationToken);
            },
            cancellationToken));
        return command;
    }

    private static async Task<int> ExecuteReservationAsync(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        ParseResult parseResult,
        string operationName,
        AdminPermission permission,
        Func<IServiceProvider, Task<Result<ReservationDto>>> execute,
        CancellationToken cancellationToken)
    {
        AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
        return await executor.ExecuteAsync(
            parseResult,
            AdminOperation.Create(operationName, permission),
            parseResult.GetValue(globalOptions.TenantOption),
            requireTenant: true,
            async (provider, _) =>
            {
                Result<ReservationDto> result = await execute(provider).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    WriteReservations([result.Value], parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static void WriteReservations(IReadOnlyCollection<ReservationDto> reservations, string output) =>
        AdminCliOutput.WriteRows(
            reservations,
            output,
            [
                ("ReservationId", reservation => reservation.ReservationId.ToString()),
                ("PropertyId", reservation => reservation.PropertyId.ToString()),
                ("Arrival", reservation => reservation.Arrival.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("Departure", reservation => reservation.Departure.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("ExpectedArrival", reservation => reservation.ExpectedArrivalTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty),
                ("ExpectedDeparture", reservation => reservation.ExpectedDepartureTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty),
                ("Guest", reservation => reservation.PrimaryGuestName),
                ("Status", reservation => reservation.Status.ToString()),
                ("Units", reservation => string.Join(',', reservation.InventoryUnitIds)),
                ("Version", reservation => reservation.Version.ToString(CultureInfo.InvariantCulture))
            ]);

    private static Option<Guid> PropertyOption() => new("--property-id") { Required = true };
    private static Option<Guid> ReservationOption() => new("--reservation-id") { Required = true };
    private static Option<string> RequiredString(string name) => new(name) { Required = true };

    private static string ResolveActor(ParseResult parseResult, AdminCliGlobalOptions globalOptions) =>
        string.IsNullOrWhiteSpace(parseResult.GetValue(globalOptions.ActorOption))
            ? $"{Environment.UserDomainName}\\{Environment.UserName}"
            : parseResult.GetValue(globalOptions.ActorOption)!.Trim();

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryParseOptionalTime(string? value, out TimeOnly? time)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            time = null;
            return true;
        }

        bool parsed = TimeOnly.TryParseExact(
            value.Trim(),
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out TimeOnly result);
        time = parsed ? result : null;
        return parsed;
    }

    private static bool TryParseUnitIds(string value, out Guid[] unitIds)
    {
        string[] values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        unitIds = values.Select(item => Guid.TryParse(item, out Guid id) ? id : Guid.Empty).ToArray();
        return unitIds.Length is > 0 and <= 100 && unitIds.All(id => id != Guid.Empty) && unitIds.Distinct().Count() == unitIds.Length;
    }
}
