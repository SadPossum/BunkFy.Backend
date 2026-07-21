namespace BunkFy.Modules.Workspaces.AdminCli;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using BunkFy.Modules.Workspaces.Admin.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Results;
using Gma.Modules.Auth.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class WorkspacesAdminCliModule : IAdminCliModule
{
    public string Name => WorkspacesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(WorkspacesProfiles.Default, "BunkFy.Modules.Workspaces.AdminCli");
        string globalAuthScopeId = builder.Configuration["Auth:GlobalScopeId"] ??
            AuthProfile.DefaultGlobalScopeId;
        builder.Services.AddWorkspacesApplication(globalAuthScopeId);
        builder.AddWorkspacesPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions global = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command access = new("access", "Inspect and migrate workspace access profiles.")
        {
            CreateStatusCommand(commands.Services, global),
            CreateBootstrapCommand(commands.Services, global)
        };
        Command module = new(WorkspacesModuleMetadata.Name, "Workspace composition administration operations.")
        {
            access
        };
        commands.AddCommand(this.Name, module);
    }

    private static Command CreateStatusCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Command command = new("status", "Inspect access seed and legacy-member migration status.");
        command.SetAction((parse, cancellationToken) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parse,
            AdminOperation.Create(
                WorkspacesAdminOperationNames.AccessBootstrapStatus,
                WorkspacesAdminPermissions.AccessBootstrap),
            parse.GetValue(global.TenantOption),
            requireTenant: true,
            async (provider, token) =>
            {
                Result<WorkspaceAccessBootstrapStatus> result = await provider
                    .GetRequiredService<IRequestDispatcher>()
                    .QueryAsync(new GetWorkspaceAccessBootstrapStatusQuery(), token)
                    .ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    WriteStatus(result.Value, parse.GetValue(global.OutputOption) ?? AdminCliOutput.Table);
                }

                return result;
            },
            cancellationToken));
        return command;
    }

    private static Command CreateBootstrapCommand(IServiceProvider services, AdminCliGlobalOptions global)
    {
        Option<bool> yes = new("--yes");
        Command command = new("bootstrap", "Seed profiles and migrate every legacy member in the workspace.")
        {
            yes
        };
        command.SetAction((parse, cancellationToken) => services.GetRequiredService<AdminCliExecutor>().ExecuteAsync(
            parse,
            AdminOperation.Create(
                WorkspacesAdminOperationNames.AccessBootstrapRun,
                WorkspacesAdminPermissions.AccessBootstrap),
            parse.GetValue(global.TenantOption),
            requireTenant: true,
            async (provider, token) =>
            {
                Result<WorkspaceAccessBootstrapResult> result = parse.GetValue(yes)
                    ? await provider.GetRequiredService<IRequestDispatcher>()
                        .SendAsync(new BootstrapWorkspaceAccessCommand(), token)
                        .ConfigureAwait(false)
                    : Result.Failure<WorkspaceAccessBootstrapResult>(AdminErrors.ConfirmationRequired);
                if (result.IsSuccess)
                {
                    AdminCliOutput.WriteMessage(
                        $"Workspace access seed v{result.Value.SeedVersion} is ready; " +
                        $"migrated {result.Value.MigratedMemberCount} legacy member(s).");
                }

                return result;
            },
            cancellationToken));
        return command;
    }

    private static void WriteStatus(WorkspaceAccessBootstrapStatus status, string output) =>
        AdminCliOutput.WriteRows(
            [status],
            output,
            [
                ("SeedVersion", item => item.SeedVersion.ToString(CultureInfo.InvariantCulture)),
                ("ExpectedSeeds", item => item.ExpectedSeedProfileCount.ToString(CultureInfo.InvariantCulture)),
                ("ActiveSeeds", item => item.ActiveSeedProfileCount.ToString(CultureInfo.InvariantCulture)),
                ("ArchivedSeeds", item => item.ArchivedSeedProfileCount.ToString(CultureInfo.InvariantCulture)),
                ("LegacyMembers", item => item.LegacyMemberCount.ToString(CultureInfo.InvariantCulture)),
                ("MarkerMembers", item => item.MarkerMemberCount.ToString(CultureInfo.InvariantCulture)),
                ("RequiresBackfill", item => item.RequiresBackfill.ToString())
            ]);
}
