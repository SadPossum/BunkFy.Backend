using Gma.Modules.AccessControl.AdminCli;
using Gma.Modules.Administration.AdminCli;
using Gma.Modules.Auth.AdminCli;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.TaskRuntime.AdminCli;
using Gma.Modules.Organizations.AdminCli;
using BunkFy.Modules.Properties.AdminCli;
using BunkFy.Modules.Inventory.AdminCli;
using BunkFy.Modules.Reservations.AdminCli;
using BunkFy.Modules.Guests.AdminCli;
using BunkFy.Modules.Staff.AdminCli;
using BunkFy.Modules.Ingestion.AdminCli;
using BunkFy.Adapters.FakeHttp;
using BunkFy.Adapters.ImapReservationMail;
using BunkFy.Adapters.JsonFileDrop;
using BunkFy.Parsers.ReservationMail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Caching.Redis;
using Gma.Framework.Infrastructure;
using Gma.Framework.FileManagement.Minio;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using System.CommandLine;
using System.CommandLine.Parsing;

try
{
    HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
        new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
    string authScopeId = builder.Configuration["Auth:GlobalScopeId"] ?? AuthProfile.DefaultGlobalScopeId;

    builder.Services.AddGmaAdministrationCli();
    builder.AddRedisCaching();
    builder.AddCachingCqrs();
    builder.AddGmaInfrastructure();
    builder.Services.AddFakeHttpAdapterDescriptor();
    builder.Services.AddImapReservationMailAdapterDescriptor();
    builder.Services.AddJsonFileDropAdapterDescriptor();
    builder.Services.AddReservationMailParserDescriptor();
    builder.AddMinioFileStorage();
    builder.AddTenantCaching();
    builder.AddMessagingInfrastructure();
    builder.AddTenantAwareMessaging();
    builder.AddAdminModule<AdministrationAdminCliModule>();
    builder.AddAdminModule<AccessControlAdminCliModule>();
    builder.AddAuthAdminModule(AuthProfile.Global(authScopeId));
    builder.AddAdminModule<OrganizationsAdminCliModule>();
    builder.AddAdminModule<TaskRuntimeAdminCliModule>();
    builder.AddAdminModule<PropertiesAdminCliModule>();
    builder.AddAdminModule<InventoryAdminCliModule>();
    builder.AddAdminModule<ReservationsAdminCliModule>();
    builder.AddAdminModule<GuestsAdminCliModule>();
    builder.AddAdminModule<StaffAdminCliModule>();
    builder.AddAdminModule<IngestionAdminCliModule>();
    builder.ValidateModuleComposition();

    using IHost host = builder.Build();

    host.Services.ValidateAdminCliStartup();
    RootCommand rootCommand = host.Services.CreateAdminRootCommand();
    ParseResult parseResult = rootCommand.Parse(args);
    InvocationConfiguration invocation = new()
    {
        EnableDefaultExceptionHandler = false
    };

    return await parseResult.InvokeAsync(invocation, CancellationToken.None).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    AdminCliOutput.WriteError("Admin command was canceled.");
    return AdminExitCodes.Failed;
}
catch (OptionsValidationException exception)
{
    AdminCliOutput.WriteError("Admin CLI configuration is invalid.");

    foreach (string failure in exception.Failures.Distinct(StringComparer.Ordinal))
    {
        AdminCliOutput.WriteError(failure);
    }

    return AdminExitCodes.Failed;
}
catch (ModuleCompositionValidationException exception)
{
    AdminCliOutput.WriteError("Admin CLI module composition is invalid.");

    foreach (string error in exception.Errors)
    {
        AdminCliOutput.WriteError(error);
    }

    return AdminExitCodes.Failed;
}
catch (Exception)
{
    AdminCliOutput.WriteError("Admin command failed unexpectedly.");
    return AdminExitCodes.Failed;
}
