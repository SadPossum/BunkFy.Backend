namespace Properties.AdminCli;

using System.Globalization;
using System.CommandLine;
using System.CommandLine.Parsing;
using Properties.Admin.Contracts;
using Properties.Application;
using Properties.Application.Commands;
using Properties.Application.Queries;
using Properties.Contracts;
using Properties.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

public sealed class PropertiesAdminCliModule : IAdminCliModule
{
    public string Name => PropertiesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(PropertiesProfiles.Default, "Properties.AdminCli");
        builder.Services.AddPropertiesApplication();
        builder.AddPropertiesPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command properties = new(PropertiesModuleMetadata.Name, "Property setup administration operations.")
        {
            CreateListPropertiesCommand(commands.Services, globalOptions),
            CreateGetPropertyCommand(commands.Services, globalOptions),
            CreateCreatePropertyCommand(commands.Services, globalOptions),
            CreateUpdatePropertyCommand(commands.Services, globalOptions),
            CreateRetirePropertyCommand(commands.Services, globalOptions),
            new Command("rooms", "Manage property rooms.")
            {
                CreateListRoomsCommand(commands.Services, globalOptions),
                CreateGetRoomCommand(commands.Services, globalOptions),
                CreateCreateRoomCommand(commands.Services, globalOptions),
                CreateUpdateRoomCommand(commands.Services, globalOptions),
                CreateRetireRoomCommand(commands.Services, globalOptions)
            },
            new Command("beds", "Manage room beds.")
            {
                CreateListBedsCommand(commands.Services, globalOptions),
                CreateAddBedCommand(commands.Services, globalOptions),
                CreateUpdateBedCommand(commands.Services, globalOptions),
                CreateRetireBedCommand(commands.Services, globalOptions)
            }
        };

        commands.AddCommand(this.Name, properties);
    }

    private static Command CreateListPropertiesCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<int> pageOption = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSizeOption = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List properties.") { pageOption, pageSizeOption };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesList, PropertiesAdminPermissions.Read),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<PropertyListResponse> result = await dispatcher.QueryAsync(
                        new ListPropertiesQuery(parseResult.GetValue(pageOption), parseResult.GetValue(pageSizeOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteRows(
                            result.Value.Properties,
                            parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table,
                            [
                                ("PropertyId", property => property.PropertyId.ToString()),
                                ("Code", property => property.Code),
                                ("Name", property => property.Name),
                                ("TimeZone", property => property.TimeZoneId),
                                ("Status", property => property.Status.ToString()),
                                ("Version", property => property.Version.ToString(CultureInfo.InvariantCulture))
                            ]);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateGetPropertyCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Command command = new("get", "Get a property.") { propertyIdOption };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesGet, PropertiesAdminPermissions.Read),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<PropertyDto> result = await dispatcher.QueryAsync(
                        new GetPropertyQuery(parseResult.GetRequiredValue(propertyIdOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteRows(
                            [result.Value],
                            parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table,
                            [
                                ("PropertyId", property => property.PropertyId.ToString()),
                                ("Code", property => property.Code),
                                ("Name", property => property.Name),
                                ("TimeZone", property => property.TimeZoneId),
                                ("Status", property => property.Status.ToString()),
                                ("Version", property => property.Version.ToString(CultureInfo.InvariantCulture))
                            ]);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateCreatePropertyCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<string> nameOption = new("--name") { Required = true };
        Option<string> codeOption = new("--code") { Required = true };
        Option<string> timeZoneOption = new("--time-zone") { DefaultValueFactory = _ => "UTC" };
        Command command = new("create", "Create a property.") { nameOption, codeOption, timeZoneOption };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesCreate, PropertiesAdminPermissions.PropertiesManage),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<PropertyDto> result = await dispatcher.SendAsync(
                        new CreatePropertyCommand(
                            parseResult.GetRequiredValue(nameOption),
                            parseResult.GetRequiredValue(codeOption),
                            parseResult.GetValue(timeZoneOption) ?? "UTC"),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage($"Created property '{result.Value.PropertyId}'.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateUpdatePropertyCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<string> nameOption = new("--name") { Required = true };
        Option<string> codeOption = new("--code") { Required = true };
        Option<string> timeZoneOption = new("--time-zone") { DefaultValueFactory = _ => "UTC" };
        Option<long> expectedVersionOption = CreateRequiredVersionOption("--expected-version");
        Command command = new("update", "Update a property.")
        {
            propertyIdOption,
            nameOption,
            codeOption,
            timeZoneOption,
            expectedVersionOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesUpdate, PropertiesAdminPermissions.PropertiesManage),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<PropertyDto> result = await dispatcher.SendAsync(
                        new UpdatePropertyCommand(
                            parseResult.GetRequiredValue(propertyIdOption),
                            parseResult.GetRequiredValue(nameOption),
                            parseResult.GetRequiredValue(codeOption),
                            parseResult.GetValue(timeZoneOption) ?? "UTC",
                            parseResult.GetRequiredValue(expectedVersionOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Property updated.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateRetirePropertyCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<long> expectedVersionOption = CreateRequiredVersionOption("--expected-version");
        Option<bool> yesOption = new("--yes");
        Command command = new("retire", "Retire a property after all rooms are retired.")
        {
            propertyIdOption,
            expectedVersionOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteUnitCommandAsync(
                services,
                globalOptions,
                parseResult,
                PropertiesAdminOperationNames.PropertiesRetire,
                PropertiesAdminPermissions.PropertiesManage,
                provider => parseResult.GetValue(yesOption)
                    ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                        new RetirePropertyCommand(
                            parseResult.GetRequiredValue(propertyIdOption),
                            parseResult.GetRequiredValue(expectedVersionOption)),
                        cancellationToken)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                "Property retired.",
                cancellationToken));
        return command;
    }

    private static Command CreateListRoomsCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<int> pageOption = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSizeOption = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List rooms.") { propertyIdOption, pageOption, pageSizeOption };
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteRoomsQueryAsync(
                services,
                globalOptions,
                parseResult,
                PropertiesAdminOperationNames.RoomsList,
                PropertiesAdminPermissions.Read,
                provider => provider.GetRequiredService<IRequestDispatcher>().QueryAsync(
                    new ListRoomsQuery(
                        parseResult.GetRequiredValue(propertyIdOption),
                        parseResult.GetValue(pageOption),
                        parseResult.GetValue(pageSizeOption)),
                    cancellationToken),
                cancellationToken));
        return command;
    }

    private static Command CreateGetRoomCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<Guid> roomIdOption = CreateRoomIdOption();
        Command command = new("get", "Get a room.") { propertyIdOption, roomIdOption };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsGet, PropertiesAdminPermissions.Read),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<RoomDto> result = await dispatcher.QueryAsync(
                        new GetRoomQuery(
                            parseResult.GetRequiredValue(propertyIdOption),
                            parseResult.GetRequiredValue(roomIdOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        WriteRooms([result.Value], parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                    }

                    return result;
                },
                cancellationToken);
        });
        return command;
    }

    private static Command CreateCreateRoomCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<long> expectedPropertyVersionOption = CreateRequiredVersionOption("--expected-property-version");
        Option<string> nameOption = new("--name") { Required = true };
        Option<string> buildingOption = new("--building");
        Option<string> floorOption = new("--floor");
        Command command = new("create", "Create a room.")
        {
            propertyIdOption,
            expectedPropertyVersionOption,
            nameOption,
            buildingOption,
            floorOption
        };
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteRoomCommandAsync(
                services,
                globalOptions,
                parseResult,
                PropertiesAdminOperationNames.RoomsCreate,
                PropertiesAdminPermissions.RoomsManage,
                provider => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new CreateRoomCommand(
                        parseResult.GetRequiredValue(propertyIdOption),
                        parseResult.GetRequiredValue(expectedPropertyVersionOption),
                        parseResult.GetRequiredValue(nameOption),
                        parseResult.GetValue(buildingOption),
                        parseResult.GetValue(floorOption)),
                    cancellationToken),
                "Created room",
                cancellationToken));
        return command;
    }

    private static Command CreateUpdateRoomCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<Guid> roomIdOption = CreateRoomIdOption();
        Option<long> expectedVersionOption = CreateRequiredVersionOption("--expected-version");
        Option<string> nameOption = new("--name") { Required = true };
        Option<string> buildingOption = new("--building");
        Option<string> floorOption = new("--floor");
        Command command = new("update", "Update a room.")
        {
            propertyIdOption,
            roomIdOption,
            expectedVersionOption,
            nameOption,
            buildingOption,
            floorOption
        };
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteRoomCommandAsync(
                services,
                globalOptions,
                parseResult,
                PropertiesAdminOperationNames.RoomsUpdate,
                PropertiesAdminPermissions.RoomsManage,
                provider => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new UpdateRoomCommand(
                        parseResult.GetRequiredValue(propertyIdOption),
                        parseResult.GetRequiredValue(roomIdOption),
                        parseResult.GetRequiredValue(expectedVersionOption),
                        parseResult.GetRequiredValue(nameOption),
                        parseResult.GetValue(buildingOption),
                        parseResult.GetValue(floorOption)),
                    cancellationToken),
                "Room updated",
                cancellationToken));
        return command;
    }

    private static Command CreateRetireRoomCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<Guid> roomIdOption = CreateRoomIdOption();
        Option<long> expectedVersionOption = CreateRequiredVersionOption("--expected-version");
        Option<bool> cascadeBedsOption = new("--cascade-beds");
        Option<bool> yesOption = new("--yes");
        Command command = new("retire", "Retire a room.")
        {
            propertyIdOption,
            roomIdOption,
            expectedVersionOption,
            cascadeBedsOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteUnitCommandAsync(
                services,
                globalOptions,
                parseResult,
                PropertiesAdminOperationNames.RoomsRetire,
                PropertiesAdminPermissions.RoomsManage,
                provider => parseResult.GetValue(yesOption)
                    ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                        new RetireRoomCommand(
                            parseResult.GetRequiredValue(propertyIdOption),
                            parseResult.GetRequiredValue(roomIdOption),
                            parseResult.GetRequiredValue(expectedVersionOption),
                            parseResult.GetValue(cascadeBedsOption)),
                        cancellationToken)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                "Room retired.",
                cancellationToken));
        return command;
    }

    private static Command CreateListBedsCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<Guid> roomIdOption = CreateRoomIdOption();
        Option<int> pageOption = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSizeOption = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List beds.") { propertyIdOption, roomIdOption, pageOption, pageSizeOption };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(PropertiesAdminOperationNames.BedsList, PropertiesAdminPermissions.Read),
                parseResult.GetValue(globalOptions.TenantOption),
                requireTenant: true,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<BedListResponse> result = await dispatcher.QueryAsync(
                        new ListBedsQuery(
                            parseResult.GetRequiredValue(propertyIdOption),
                            parseResult.GetRequiredValue(roomIdOption),
                            parseResult.GetValue(pageOption),
                            parseResult.GetValue(pageSizeOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        WriteBeds(result.Value.Beds, parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                    }

                    return result;
                },
                cancellationToken);
        });
        return command;
    }

    private static Command CreateAddBedCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<Guid> roomIdOption = CreateRoomIdOption();
        Option<long> expectedRoomVersionOption = CreateRequiredVersionOption("--expected-room-version");
        Option<string> labelOption = CreateBedLabelOption();
        Command command = new("add", "Add a bed.")
        {
            propertyIdOption,
            roomIdOption,
            expectedRoomVersionOption,
            labelOption
        };
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteBedCommandAsync(
                services,
                globalOptions,
                parseResult,
                PropertiesAdminOperationNames.BedsAdd,
                PropertiesAdminPermissions.BedsManage,
                provider => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new AddBedCommand(
                        parseResult.GetRequiredValue(propertyIdOption),
                        parseResult.GetRequiredValue(roomIdOption),
                        parseResult.GetRequiredValue(expectedRoomVersionOption),
                        parseResult.GetRequiredValue(labelOption)),
                    cancellationToken),
                "Added bed",
                cancellationToken));
        return command;
    }

    private static Command CreateUpdateBedCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<Guid> roomIdOption = CreateRoomIdOption();
        Option<Guid> bedIdOption = CreateBedIdOption();
        Option<long> expectedRoomVersionOption = CreateRequiredVersionOption("--expected-room-version");
        Option<string> labelOption = CreateBedLabelOption();
        Command command = new("update", "Update a bed.")
        {
            propertyIdOption,
            roomIdOption,
            bedIdOption,
            expectedRoomVersionOption,
            labelOption
        };
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteBedCommandAsync(
                services,
                globalOptions,
                parseResult,
                PropertiesAdminOperationNames.BedsUpdate,
                PropertiesAdminPermissions.BedsManage,
                provider => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new UpdateBedCommand(
                        parseResult.GetRequiredValue(propertyIdOption),
                        parseResult.GetRequiredValue(roomIdOption),
                        parseResult.GetRequiredValue(bedIdOption),
                        parseResult.GetRequiredValue(expectedRoomVersionOption),
                        parseResult.GetRequiredValue(labelOption)),
                    cancellationToken),
                "Bed updated",
                cancellationToken));
        return command;
    }

    private static Command CreateRetireBedCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> propertyIdOption = CreatePropertyIdOption();
        Option<Guid> roomIdOption = CreateRoomIdOption();
        Option<Guid> bedIdOption = CreateBedIdOption();
        Option<long> expectedRoomVersionOption = CreateRequiredVersionOption("--expected-room-version");
        Option<bool> yesOption = new("--yes");
        Command command = new("retire", "Retire a bed.")
        {
            propertyIdOption,
            roomIdOption,
            bedIdOption,
            expectedRoomVersionOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
            ExecuteUnitCommandAsync(
                services,
                globalOptions,
                parseResult,
                PropertiesAdminOperationNames.BedsRetire,
                PropertiesAdminPermissions.BedsManage,
                provider => parseResult.GetValue(yesOption)
                    ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                        new RetireBedCommand(
                            parseResult.GetRequiredValue(propertyIdOption),
                            parseResult.GetRequiredValue(roomIdOption),
                            parseResult.GetRequiredValue(bedIdOption),
                            parseResult.GetRequiredValue(expectedRoomVersionOption)),
                        cancellationToken)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                "Bed retired.",
                cancellationToken));
        return command;
    }

    private static async Task<int> ExecuteRoomsQueryAsync(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        ParseResult parseResult,
        string operationName,
        AdminPermission permission,
        Func<IServiceProvider, Task<Result<RoomListResponse>>> execute,
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
                Result<RoomListResponse> result = await execute(provider).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    WriteRooms(result.Value.Rooms, parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ExecuteRoomCommandAsync(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        ParseResult parseResult,
        string operationName,
        AdminPermission permission,
        Func<IServiceProvider, Task<Result<RoomDto>>> execute,
        string successPrefix,
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
                Result<RoomDto> result = await execute(provider).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    AdminCliOutput.WriteMessage($"{successPrefix} '{result.Value.RoomId}'.");
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ExecuteBedCommandAsync(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        ParseResult parseResult,
        string operationName,
        AdminPermission permission,
        Func<IServiceProvider, Task<Result<BedDto>>> execute,
        string successPrefix,
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
                Result<BedDto> result = await execute(provider).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    AdminCliOutput.WriteMessage($"{successPrefix} '{result.Value.BedId}'.");
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ExecuteUnitCommandAsync(
        IServiceProvider services,
        AdminCliGlobalOptions globalOptions,
        ParseResult parseResult,
        string operationName,
        AdminPermission permission,
        Func<IServiceProvider, Task<Result<Unit>>> execute,
        string successMessage,
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
                Result<Unit> result = await execute(provider).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    AdminCliOutput.WriteMessage(successMessage);
                }

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static void WriteRooms(IReadOnlyCollection<RoomDto> rooms, string output) =>
        AdminCliOutput.WriteRows(
            rooms,
            output,
            [
                ("RoomId", room => room.RoomId.ToString()),
                ("PropertyId", room => room.PropertyId.ToString()),
                ("Name", room => room.Name),
                ("Building", room => room.BuildingLabel ?? string.Empty),
                ("Floor", room => room.FloorLabel ?? string.Empty),
                ("Status", room => room.Status.ToString()),
                ("Version", room => room.Version.ToString(CultureInfo.InvariantCulture))
            ]);

    private static void WriteBeds(IReadOnlyCollection<BedDto> beds, string output) =>
        AdminCliOutput.WriteRows(
            beds,
            output,
            [
                ("BedId", bed => bed.BedId.ToString()),
                ("RoomId", bed => bed.RoomId.ToString()),
                ("PropertyId", bed => bed.PropertyId.ToString()),
                ("Label", bed => bed.Label),
                ("Status", bed => bed.Status.ToString()),
                ("Version", bed => bed.Version.ToString(CultureInfo.InvariantCulture)),
                ("RoomVersion", bed => bed.RoomVersion.ToString(CultureInfo.InvariantCulture))
            ]);

    private static Option<Guid> CreatePropertyIdOption() =>
        new("--property-id") { Required = true };

    private static Option<Guid> CreateRoomIdOption() =>
        new("--room-id") { Required = true };

    private static Option<Guid> CreateBedIdOption() =>
        new("--bed-id") { Required = true };

    private static Option<string> CreateBedLabelOption() =>
        new("--label") { Required = true };

    private static Option<long> CreateRequiredVersionOption(string name) =>
        new(name) { Required = true };
}
