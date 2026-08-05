namespace BunkFy.Modules.DataRights.AdminCli;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Extensions.DataRights.TenantTermination;
using Gma.Framework.Administration.Cli;
using Gma.Framework.ModuleComposition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

public sealed class DataRightsAdminCliModule : IAdminCliModule
{
    public string Name => DataRightsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(
            DataRightsProfiles.Default,
            "BunkFy.Modules.DataRights.AdminCli");
        builder.Services.AddDataRightsApplication();
        builder.Services.AddDataRightsTenantTerminationTaskScheduling();
        builder.Services.AddBunkFyTenantTerminationOperatorCatalog();
        builder.AddDataRightsPersistence();
        builder.AddDataRightsRestoreReadinessGate();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions =
            commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command module = new(
            DataRightsModuleMetadata.Name,
            "DataRights administration operations.")
        {
            TenantTerminationAdminCliCommandMap.Create(
                commands.Services,
                globalOptions)
        };
        commands.AddCommand(this.Name, module);
    }
}
