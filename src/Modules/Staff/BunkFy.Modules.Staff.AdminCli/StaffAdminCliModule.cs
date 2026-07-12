namespace BunkFy.Modules.Staff.AdminCli;

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
using BunkFy.Modules.Staff.Admin.Contracts;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Persistence;

public sealed class StaffAdminCliModule : IAdminCliModule
{
    public string Name => StaffModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(StaffProfiles.Default, "BunkFy.Modules.Staff.AdminCli");
        builder.Services.AddStaffApplication();
        builder.AddStaffPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions global = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command module = new(StaffModuleMetadata.Name, "Staff profile administration operations.")
        {
            CreateListCommand(commands.Services, global), CreateGetCommand(commands.Services, global),
            CreateCreateCommand(commands.Services, global), CreateUpdateCommand(commands.Services, global),
            CreateAuthSubjectCommand(commands.Services, global), CreateAssignmentCommand(commands.Services, global),
            CreateUnassignmentCommand(commands.Services, global),
            CreateLifecycleCommand(commands.Services, global, "suspend"),
            CreateLifecycleCommand(commands.Services, global, "resume"),
            CreateDepartureCommand(commands.Services, global)
        };
        commands.AddCommand(this.Name, module);
    }

    private static Command CreateListCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Option<string?> search = new("--search");
        Option<string?> status = new("--status");
        Option<int> page = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSize = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List tenant staff profiles.") { search, status, page, pageSize };
        command.SetAction((parse, token) => ExecuteAsync(services, global, parse,
            StaffAdminOperationNames.List, StaffAdminPermissions.Read, async (provider, ct) =>
            {
                string? text = parse.GetValue(status);
                StaffStatus? parsed = string.IsNullOrWhiteSpace(text) ? null :
                    Enum.TryParse(text, true, out StaffStatus value) ? value : StaffStatus.Unknown;
                Result<StaffListResponse> result = await provider.GetRequiredService<IRequestDispatcher>()
                    .QueryAsync(new ListStaffMembersQuery(parse.GetValue(search), parsed,
                        parse.GetValue(page), parse.GetValue(pageSize)), ct).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    Write(result.Value.Items, Output(parse, global));
                }

                return result;
            }, token));
        return command;
    }

    private static Command CreateGetCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Option<Guid> member = MemberOption();
        Command command = new("get", "Get a staff profile.") { member };
        command.SetAction((parse, token) => ExecuteProfileAsync(services, global, parse,
            StaffAdminOperationNames.Get, StaffAdminPermissions.Read,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>()
                .QueryAsync(new GetStaffMemberQuery(parse.GetRequiredValue(member)), ct), token));
        return command;
    }

    private static Command CreateCreateCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        ProfileOptions options = new();
        Command command = new("create", "Create a staff profile.");
        options.AddTo(command, false);
        command.SetAction((parse, token) => ExecuteProfileAsync(services, global, parse,
            StaffAdminOperationNames.Create, StaffAdminPermissions.Create,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                new CreateStaffMemberCommand(parse.GetRequiredValue(options.DisplayName),
                    parse.GetValue(options.LegalName), parse.GetValue(options.Email),
                    parse.GetValue(options.Phone), parse.GetValue(options.EmployeeNumber),
                    parse.GetValue(options.JobTitle), parse.GetValue(options.Department),
                    parse.GetValue(options.AuthSubject), Actor(parse, global)), ct), token));
        return command;
    }

    private static Command CreateUpdateCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Option<Guid> member = MemberOption();
        ProfileOptions options = new();
        Command command = new("update", "Update a staff profile.") { member };
        options.AddTo(command, true);
        command.SetAction((parse, token) => ExecuteProfileAsync(services, global, parse,
            StaffAdminOperationNames.Update, StaffAdminPermissions.Manage,
            (provider, ct) => provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                new UpdateStaffMemberCommand(parse.GetRequiredValue(member),
                    parse.GetRequiredValue(options.DisplayName), parse.GetValue(options.LegalName),
                    parse.GetValue(options.Email), parse.GetValue(options.Phone),
                    parse.GetValue(options.EmployeeNumber), parse.GetValue(options.JobTitle),
                    parse.GetValue(options.Department), parse.GetRequiredValue(options.Version),
                    Actor(parse, global)), ct), token));
        return command;
    }

    private static Command CreateAuthSubjectCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Option<Guid> member = MemberOption();
        Option<string?> subject = new("--auth-subject-id");
        Option<long> version = VersionOption();
        Option<bool> yes = new("--yes");
        Command command = new("set-auth-subject", "Link, replace, or clear an Auth user subject.")
            { member, subject, version, yes };
        command.SetAction((parse, token) => ExecuteProfileAsync(services, global, parse,
            StaffAdminOperationNames.SetAuthSubject, StaffAdminPermissions.Manage,
            (provider, ct) => parse.GetValue(yes)
                ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(
                    new SetStaffAuthSubjectCommand(parse.GetRequiredValue(member), parse.GetValue(subject),
                        parse.GetRequiredValue(version), Actor(parse, global)), ct)
                : Task.FromResult(Result.Failure<StaffMemberDto>(AdminErrors.ConfirmationRequired)), token));
        return command;
    }

    private static Command CreateAssignmentCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Option<Guid> member = MemberOption();
        Option<Guid> property = PropertyOption();
        Option<string?> title = new("--property-job-title");
        Option<bool> primary = new("--primary");
        Option<string> effective = DateOption("--effective-from");
        Option<long> version = VersionOption();
        Command command = new("assign-property", "Assign a staff member to a property.")
            { member, property, title, primary, effective, version };
        command.SetAction((parse, token) => ExecuteProfileAsync(services, global, parse,
            StaffAdminOperationNames.AssignProperty, StaffAdminPermissions.AssignProperties,
            (provider, ct) => TryDate(parse.GetRequiredValue(effective), out DateOnly date)
                ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(new AssignStaffPropertyCommand(
                    parse.GetRequiredValue(member), parse.GetRequiredValue(property), parse.GetValue(title),
                    parse.GetValue(primary), date, parse.GetRequiredValue(version), Actor(parse, global)), ct)
                : Task.FromResult(Result.Failure<StaffMemberDto>(InvalidDateError)), token));
        return command;
    }

    private static Command CreateUnassignmentCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Option<Guid> member = MemberOption();
        Option<Guid> property = PropertyOption();
        Option<string> effective = DateOption("--effective-to");
        Option<string> reason = ReasonOption();
        Option<long> version = VersionOption();
        Command command = new("unassign-property", "End a staff property assignment.")
            { member, property, effective, reason, version };
        command.SetAction((parse, token) => ExecuteProfileAsync(services, global, parse,
            StaffAdminOperationNames.UnassignProperty, StaffAdminPermissions.AssignProperties,
            (provider, ct) => TryDate(parse.GetRequiredValue(effective), out DateOnly date)
                ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(new UnassignStaffPropertyCommand(
                    parse.GetRequiredValue(member), parse.GetRequiredValue(property), date,
                    parse.GetRequiredValue(reason), parse.GetRequiredValue(version), Actor(parse, global)), ct)
                : Task.FromResult(Result.Failure<StaffMemberDto>(InvalidDateError)), token));
        return command;
    }

    private static Command CreateLifecycleCommand(IServiceProvider services, AdminCliGlobalOptions global, string name)
    {
        Option<Guid> member = MemberOption();
        Option<string> reason = ReasonOption();
        Option<long> version = VersionOption();
        Command command = new(name, $"{char.ToUpperInvariant(name[0])}{name[1..]} a staff member.")
            { member, reason, version };
        bool suspend = name == "suspend";
        command.SetAction((parse, token) => ExecuteProfileAsync(services, global, parse,
            suspend ? StaffAdminOperationNames.Suspend : StaffAdminOperationNames.Resume,
            StaffAdminPermissions.ManageLifecycle,
            (provider, ct) => suspend
                ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(new SuspendStaffMemberCommand(
                    parse.GetRequiredValue(member), parse.GetRequiredValue(reason),
                    parse.GetRequiredValue(version), Actor(parse, global)), ct)
                : provider.GetRequiredService<IRequestDispatcher>().SendAsync(new ResumeStaffMemberCommand(
                    parse.GetRequiredValue(member), parse.GetRequiredValue(reason),
                    parse.GetRequiredValue(version), Actor(parse, global)), ct), token));
        return command;
    }

    private static Command CreateDepartureCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Option<Guid> member = MemberOption();
        Option<string> effective = DateOption("--effective-on");
        Option<string> reason = ReasonOption();
        Option<long> version = VersionOption();
        Option<bool> yes = new("--yes");
        Command command = new("depart", "Mark a staff member as departed.")
            { member, effective, reason, version, yes };
        command.SetAction((parse, token) => ExecuteProfileAsync(services, global, parse,
            StaffAdminOperationNames.Depart, StaffAdminPermissions.ManageLifecycle,
            (provider, ct) => parse.GetValue(yes) && TryDate(parse.GetRequiredValue(effective), out DateOnly date)
                ? provider.GetRequiredService<IRequestDispatcher>().SendAsync(new DepartStaffMemberCommand(
                    parse.GetRequiredValue(member), date, parse.GetRequiredValue(reason),
                    parse.GetRequiredValue(version), Actor(parse, global)), ct)
                : Task.FromResult(Result.Failure<StaffMemberDto>(parse.GetValue(yes)
                    ? InvalidDateError : AdminErrors.ConfirmationRequired)), token));
        return command;
    }

    private static async Task<int> ExecuteProfileAsync(IServiceProvider services, AdminCliGlobalOptions global,
        ParseResult parse, string operation, AdminPermission permission,
        Func<IServiceProvider, CancellationToken, Task<Result<StaffMemberDto>>> action,
        CancellationToken token) => await ExecuteAsync(services, global, parse, operation, permission,
        async (provider, ct) =>
        {
            Result<StaffMemberDto> result = await action(provider, ct).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                Write([result.Value], Output(parse, global));
            }

            return result;
        }, token).ConfigureAwait(false);

    private static Task<int> ExecuteAsync<T>(IServiceProvider services, AdminCliGlobalOptions global,
        ParseResult parse, string operation, AdminPermission permission,
        Func<IServiceProvider, CancellationToken, Task<Result<T>>> action, CancellationToken token) =>
        services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(parse,
            AdminOperation.Create(operation, permission), parse.GetValue(global.TenantOption), true, action, token);

    private static void Write(IReadOnlyCollection<StaffMemberDto> profiles, string output) =>
        AdminCliOutput.WriteRows(profiles, output,
        [
            ("StaffMemberId", profile => profile.StaffMemberId.ToString()),
            ("DisplayName", profile => profile.DisplayName),
            ("EmployeeNumber", profile => profile.EmployeeNumber ?? string.Empty),
            ("JobTitle", profile => profile.JobTitle ?? string.Empty),
            ("Status", profile => profile.Status.ToString()),
            ("AuthSubjectId", profile => profile.AuthSubjectId ?? string.Empty),
            ("Assignments", profile => profile.Assignments.Count.ToString(CultureInfo.InvariantCulture)),
            ("Version", profile => profile.Version.ToString(CultureInfo.InvariantCulture))
        ]);

    private static bool TryDate(string value, out DateOnly date) => DateOnly.TryParseExact(value,
        "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static string Output(ParseResult parse, AdminCliGlobalOptions global) =>
        parse.GetValue(global.OutputOption) ?? AdminCliOutput.Table;
    private static string Actor(ParseResult parse, AdminCliGlobalOptions global) =>
        string.IsNullOrWhiteSpace(parse.GetValue(global.ActorOption))
            ? $"{Environment.UserDomainName}\\{Environment.UserName}"
            : parse.GetValue(global.ActorOption)!.Trim();
    private static Option<Guid> MemberOption() => new("--staff-member-id") { Required = true };
    private static Option<Guid> PropertyOption() => new("--property-id") { Required = true };
    private static Option<long> VersionOption() => new("--expected-version") { Required = true };
    private static Option<string> ReasonOption() => new("--reason") { Required = true };
    private static Option<string> DateOption(string name) => new(name) { Required = true };

    private static readonly Error InvalidDateError = new("Staff.DateInvalid", "Date must use yyyy-MM-dd format.");

    private sealed class ProfileOptions
    {
        public Option<string> DisplayName { get; } = new("--display-name") { Required = true };
        public Option<string?> LegalName { get; } = new("--legal-name");
        public Option<string?> Email { get; } = new("--work-email");
        public Option<string?> Phone { get; } = new("--work-phone");
        public Option<string?> EmployeeNumber { get; } = new("--employee-number");
        public Option<string?> JobTitle { get; } = new("--job-title");
        public Option<string?> Department { get; } = new("--department");
        public Option<string?> AuthSubject { get; } = new("--auth-subject-id");
        public Option<long> Version { get; } = VersionOption();

        public void AddTo(Command command, bool includeVersion)
        {
            command.Options.Add(this.DisplayName);
            command.Options.Add(this.LegalName);
            command.Options.Add(this.Email);
            command.Options.Add(this.Phone);
            command.Options.Add(this.EmployeeNumber);
            command.Options.Add(this.JobTitle);
            command.Options.Add(this.Department);
            command.Options.Add(this.AuthSubject);
            if (includeVersion)
            {
                command.Options.Add(this.Version);
            }
        }
    }
}
