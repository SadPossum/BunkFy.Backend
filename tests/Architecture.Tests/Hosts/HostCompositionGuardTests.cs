namespace Architecture.Tests.Hosts;

using Architecture.Tests.Support;
using System.Text.Json;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class HostCompositionGuardTests
{
    [Fact]
    public void Public_api_composes_reusable_and_product_modules_explicitly()
    {
        string program = RepositoryPaths.Read("src", "BunkFy.Host.Api", "Program.cs");
        string[] expectedTokens =
        [
            "builder.AddModule<TenancyModule>();",
            "builder.SelectModuleProfile(AccessControlProfiles.Default, \"BunkFy.Host.Api/AccessControl\");",
            "builder.Services.AddAccessControlApplication(builder.Configuration);",
            "builder.AddAccessControlPersistence();",
            "builder.Services.AddGmaTenantAccessControlAspNetCore();",
            "AuthProfile authProfile = AuthProfile.Global(authScopeId);",
            "builder.AddAuthModule(authProfile);",
            "builder.AddAuthTotpAuthenticator();",
            "builder.AddAuthOpenIdConnectProviders();",
            "builder.AddBunkFyDataProtection();",
            "builder.AddMinioFileStorage();",
            "builder.AddUserNotificationsRealtime();",
            "builder.AddModule<FilesModule>();",
            "builder.AddModule<NotificationsModule>();",
            "builder.AddModule<OrganizationsModule>();",
            "builder.Services.AddAuthNotificationsExtension();",
            "builder.Services.AddAuthOrganizationsExtension(options => options.GlobalAuthScopeId = authScopeId);",
            "builder.Services.AddOrganizationsTenancyExtension();",
            "builder.Services.AddBunkFyWorkspaces(options => options.GlobalAuthScopeId = authScopeId);",
            "builder.Services.AddBunkFyOperationsNotifications();",
            "builder.Services.AddNotificationEmailAdapter(builder.Configuration);",
            "builder.AddModule<PropertiesModule>();",
            "builder.AddModule<InventoryModule>();",
            "builder.AddModule<ReservationsModule>();",
            "builder.AddModule<GuestsModule>();",
            "builder.AddModule<StaffModule>();",
            "builder.AddModule<IngestionModule>();",
            "builder.AddGmaProductionHttp();",
            "app.UseGmaProductionHttp();",
            "builder.ValidateModuleComposition();",
            "app.MapModules();"
        ];

        string[] missing = expectedTokens
            .Where(token => !program.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Public_api_protects_authentication_secrets_with_a_production_durable_key_ring()
    {
        string composition = RepositoryPaths.Read(
            "src",
            "BunkFy.Host.ServiceDefaults",
            "DataProtectionExtensions.cs");
        string developmentSettings = RepositoryPaths.Read(
            "src",
            "BunkFy.Host.Api",
            "appsettings.Development.json");

        Assert.Contains("builder.Environment.IsProduction()", composition, StringComparison.Ordinal);
        Assert.Contains("PersistKeysToFileSystem", composition, StringComparison.Ordinal);
        Assert.Contains("SetApplicationName", composition, StringComparison.Ordinal);
        Assert.Contains("\"KeyRingPath\": \".data/data-protection-keys\"", developmentSettings, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_front_doors_compose_access_control_and_product_modules()
    {
        string adminApi = RepositoryPaths.Read("src", "BunkFy.Host.AdminApi", "Program.cs");
        string adminCli = RepositoryPaths.Read("src", "BunkFy.Host.AdminCli", "Program.cs");

        Assert.Contains("builder.AddAdminApiModule<AccessControlAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAuthAdminApiModule(AuthProfile.Global(authScopeId));", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<NotificationsAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<OrganizationsAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<PropertiesAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<InventoryAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<ReservationsAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<GuestsAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<StaffAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<IngestionAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminApiModule<TaskRuntimeAdminApiModule>();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddGmaProductionHttp();", adminApi, StringComparison.Ordinal);
        Assert.Contains("app.UseGmaProductionHttp();", adminApi, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<AccessControlAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAuthAdminModule(AuthProfile.Global(authScopeId));", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<OrganizationsAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<PropertiesAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<InventoryAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<ReservationsAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<GuestsAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<StaffAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<IngestionAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<TaskRuntimeAdminCliModule>();", adminCli, StringComparison.Ordinal);
    }

    [Fact]
    public void Worker_keeps_background_module_groups_opt_in()
    {
        string appsettings = RepositoryPaths.Read("src", "BunkFy.Host.Worker", "appsettings.json");
        string options = RepositoryPaths.Read("src", "BunkFy.Host.Worker", "WorkerHostOptions.cs");

        Assert.Contains("\"Auth\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"AccessControl\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Notifications\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Organizations\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Properties\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Inventory\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Reservations\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Guests\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Staff\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Ingestion\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"TaskRuntime\": false", appsettings, StringComparison.Ordinal);
        Assert.Contains("defaultValue: false", options, StringComparison.Ordinal);
        Assert.Contains(
            "AuthProfile authProfile = AuthProfile.Global(authScopeId);",
            RepositoryPaths.Read("src", "BunkFy.Host.Worker", "WorkerHostBuilderExtensions.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "NotificationsProfiles.Default",
            RepositoryPaths.Read("src", "BunkFy.Host.Worker", "WorkerHostBuilderExtensions.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Organization_retention_is_bounded_and_disabled_until_a_single_owner_is_selected()
    {
        string[] settingsPaths =
        [
            "src/BunkFy.Host.Api/appsettings.json",
            "src/BunkFy.Host.Worker/appsettings.json"
        ];

        foreach (string path in settingsPaths)
        {
            using JsonDocument document = JsonDocument.Parse(RepositoryPaths.Read(path.Split('/')));
            JsonElement retention = document.RootElement
                .GetProperty("Organizations")
                .GetProperty("Retention");

            Assert.False(retention.GetProperty("Enabled").GetBoolean());
            Assert.InRange(retention.GetProperty("InvitationHistoryDays").GetInt32(), 1, 3650);
            Assert.InRange(retention.GetProperty("EnrollmentHistoryDays").GetInt32(), 1, 3650);
            Assert.InRange(retention.GetProperty("BatchSize").GetInt32(), 1, 10000);
            Assert.InRange(retention.GetProperty("MaxBatchesPerCategoryPerCycle").GetInt32(), 1, 100);
            Assert.InRange(retention.GetProperty("IntervalMinutes").GetInt32(), 1, 1440);
        }
    }

    [Fact]
    public void Aspire_apphost_wires_infrastructure_and_optional_worker_surfaces()
    {
        string program = RepositoryPaths.Read("src", "BunkFy.Host.AppHost", "Program.cs");
        string composition = RepositoryPaths.Read(
            "src",
            "Shared",
            "BunkFy.AppHost.Composition",
            "BunkFyBackendComposition.cs");
        string appsettings = RepositoryPaths.Read("src", "BunkFy.Host.AppHost", "appsettings.json");
        string[] expectedTokens =
        [
            ".AddPostgres(\"postgres\", password: postgreSqlPassword)",
            "IsEnabled(builder, \"AppHost:SqlServer:Enabled\")",
            "builder.AddSqlServer(\"sql\")",
            "builder.AddNats(\"nats\")",
            ".AddContainer(\"minio\", \"quay.io/minio/minio\", \"latest\")",
            "FileManagement__Minio__Endpoint",
            ".AddProject(\"bunkfy-host-api\", projectPaths.Api)",
            ".WaitFor(postgreSql)",
            "Tasks__Worker__Enabled",
            "Worker__Modules__TaskRuntime",
            "Worker__Modules__Notifications",
            "Worker__Modules__AccessControl",
            "Worker__Modules__Organizations",
            "Worker__Modules__Guests",
            "Worker__Modules__Staff",
            "AppHost:AdminApi:Enabled",
            "AppHost:Worker:Enabled",
            "AppHost:Redis:Enabled"
        ];

        Assert.Contains("builder.AddBunkFyBackend(new(", program, StringComparison.Ordinal);
        string[] missing = expectedTokens
            .Where(token => !composition.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
        Assert.Contains("\"SqlServer\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Enabled\": false", appsettings, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_hosts_default_to_postgre_sql()
    {
        string[] appsettingsFiles =
        [
            "src/BunkFy.Host.Api/appsettings.json",
            "src/BunkFy.Host.AdminApi/appsettings.json",
            "src/BunkFy.Host.AdminCli/appsettings.json",
            "src/BunkFy.Host.Worker/appsettings.json"
        ];

        string[] offenders = appsettingsFiles
            .Where(path => !RepositoryPaths.Read(path.Split('/')).Contains("\"Provider\": \"PostgreSql\"", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_api_enables_global_account_and_workspace_self_registration()
    {
        string appsettings = RepositoryPaths.Read("src", "BunkFy.Host.Api", "appsettings.json");

        Assert.Contains("\"SelfRegistration\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"GlobalScopeId\": \"default\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"PasswordEnabled\": true", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"ExternalEnabled\": true", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"SelfServiceCreationEnabled\": true", appsettings, StringComparison.Ordinal);
    }

    [Fact]
    public void Workspace_membership_defaults_to_front_desk_access_without_administration()
    {
        string roles = RepositoryPaths.Read(
            "src",
            "Extensions",
            "BunkFy.Extensions.Workspaces",
            "WorkspaceAccessRoles.cs");

        Assert.Contains("PropertiesAdminPermissionCodes.Read", roles, StringComparison.Ordinal);
        Assert.Contains("InventoryAdminPermissionCodes.Read", roles, StringComparison.Ordinal);
        Assert.Contains("InventoryAdminPermissionCodes.BlocksManage", roles, StringComparison.Ordinal);
        Assert.Contains("ReservationsAdminPermissionCodes.Create", roles, StringComparison.Ordinal);
        Assert.Contains("ReservationsAdminPermissionCodes.CheckIn", roles, StringComparison.Ordinal);
        Assert.Contains("GuestsAdminPermissionCodes.Manage", roles, StringComparison.Ordinal);
        Assert.Contains("StaffAdminPermissionCodes.Read", roles, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertiesAdminPermissionCodes.Manage", roles, StringComparison.Ordinal);
        Assert.DoesNotContain("StaffAdminPermissionCodes.Manage", roles, StringComparison.Ordinal);
        Assert.DoesNotContain("IngestionAdminPermissionCodes", roles, StringComparison.Ordinal);
    }

    [Fact]
    public void Imap_adapter_metadata_and_executable_code_are_composed_in_the_correct_hosts()
    {
        string api = RepositoryPaths.Read("src", "BunkFy.Host.Api", "Program.cs");
        string adminApi = RepositoryPaths.Read("src", "BunkFy.Host.AdminApi", "Program.cs");
        string adminCli = RepositoryPaths.Read("src", "BunkFy.Host.AdminCli", "Program.cs");
        string worker = RepositoryPaths.Read("src", "BunkFy.Host.Worker", "WorkerHostBuilderExtensions.cs");
        string adapterHost = RepositoryPaths.Read("src", "BunkFy.AdapterHost", "Program.cs");

        Assert.Contains("AddImapReservationMailAdapterDescriptor();", api, StringComparison.Ordinal);
        Assert.Contains("AddImapReservationMailAdapterDescriptor();", adminApi, StringComparison.Ordinal);
        Assert.Contains("AddImapReservationMailAdapterDescriptor();", adminCli, StringComparison.Ordinal);
        Assert.DoesNotContain("AddImapReservationMailAdapter();", api, StringComparison.Ordinal);
        Assert.DoesNotContain("AddImapReservationMailAdapter();", adminApi, StringComparison.Ordinal);
        Assert.DoesNotContain("AddImapReservationMailAdapter();", adminCli, StringComparison.Ordinal);
        Assert.Contains("AddImapReservationMailAdapter();", worker, StringComparison.Ordinal);
        Assert.Contains(
            "case ImapReservationMailAdapterDescriptor.AdapterType:",
            adapterHost,
            StringComparison.Ordinal);
        Assert.Contains("AddImapReservationMailAdapter();", adapterHost, StringComparison.Ordinal);
    }

    [Fact]
    public void Observation_parser_metadata_and_executable_code_are_composed_in_the_correct_hosts()
    {
        string api = RepositoryPaths.Read("src", "BunkFy.Host.Api", "Program.cs");
        string adminApi = RepositoryPaths.Read("src", "BunkFy.Host.AdminApi", "Program.cs");
        string adminCli = RepositoryPaths.Read("src", "BunkFy.Host.AdminCli", "Program.cs");
        string worker = RepositoryPaths.Read("src", "BunkFy.Host.Worker", "WorkerHostBuilderExtensions.cs");
        string adapterHost = RepositoryPaths.Read("src", "BunkFy.AdapterHost", "Program.cs");

        Assert.Contains("AddReservationMailParserDescriptor();", api, StringComparison.Ordinal);
        Assert.Contains("AddReservationMailParserDescriptor();", adminApi, StringComparison.Ordinal);
        Assert.Contains("AddReservationMailParserDescriptor();", adminCli, StringComparison.Ordinal);
        Assert.DoesNotContain("AddReservationMailParser();", api, StringComparison.Ordinal);
        Assert.DoesNotContain("AddReservationMailParser();", adminApi, StringComparison.Ordinal);
        Assert.DoesNotContain("AddReservationMailParser();", adminCli, StringComparison.Ordinal);
        Assert.Contains("AddReservationMailParser();", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("AddReservationMailParser();", adapterHost, StringComparison.Ordinal);
    }

    [Fact]
    public void File_drop_local_retention_is_composed_in_both_executable_hosts()
    {
        string worker = RepositoryPaths.Read("src", "BunkFy.Host.Worker", "WorkerHostBuilderExtensions.cs");
        string workerSettings = RepositoryPaths.Read("src", "BunkFy.Host.Worker", "appsettings.json");
        string adapterHostOptions = RepositoryPaths.Read("src", "BunkFy.AdapterHost", "AdapterHostOptions.cs");
        string adapterHostProgram = RepositoryPaths.Read("src", "BunkFy.AdapterHost", "Program.cs");

        Assert.Contains("Adapters:JsonFileDrop:ProcessedArchiveRetention", worker, StringComparison.Ordinal);
        Assert.Contains("Adapters:JsonFileDrop:FailedQuarantineRetention", worker, StringComparison.Ordinal);
        Assert.Contains("\"RetentionEnabled\": true", workerSettings, StringComparison.Ordinal);
        Assert.Contains("JsonFileDropProcessedArchiveRetention", adapterHostOptions, StringComparison.Ordinal);
        Assert.Contains("JsonFileDropFailedQuarantineRetention", adapterHostOptions, StringComparison.Ordinal);
        Assert.Contains("options.JsonFileDropRetentionEnabled", adapterHostProgram, StringComparison.Ordinal);
    }
}
