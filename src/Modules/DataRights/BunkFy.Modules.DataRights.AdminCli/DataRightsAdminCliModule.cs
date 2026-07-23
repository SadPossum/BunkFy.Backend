namespace BunkFy.Modules.DataRights.AdminCli;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
public sealed class DataRightsAdminCliModule : IAdminCliModule
{
    public string Name => DataRightsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddDataRightsApplication();
        builder.AddDataRightsPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        Command module = new(DataRightsModuleMetadata.Name, "DataRights administration operations.");
        commands.AddCommand(this.Name, module);
    }
}
