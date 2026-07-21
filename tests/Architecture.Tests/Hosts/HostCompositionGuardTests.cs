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
            "builder.Services.AddAccessProfilePermissionAllowlist(WorkspaceAccessRoles.DelegablePermissions);",
            "options => options.ProfileManagementAssurance = privilegedOperationAssurance",
            "options => options.GovernanceOperationsAssurance = privilegedOperationAssurance",
            "options => options.CredentialManagementAssurance = privilegedOperationAssurance",
            "builder.AddModule<AccessControlApiModule>();",
            "builder.Services.AddGmaTenantAccessControlAspNetCore();",
            "AuthProfile authProfile = AuthProfile.Global(authScopeId);",
            "builder.AddAuthModule(authProfile);",
            "builder.AddAuthTotpAuthenticator();",
            "builder.AddAuthOpenIdConnectProviders();",
            "builder.AddBunkFyDataProtection();",
            "builder.AddMinioFileStorage();",
            "builder.AddUserNotificationsRealtime();",
            "builder.AddModule<NotificationsModule>();",
            "builder.AddModule<OrganizationsModule>();",
            "builder.Services.AddAuthNotificationsExtension();",
            "builder.Services.AddAuthOrganizationsExtension(options => options.GlobalAuthScopeId = authScopeId);",
            "builder.Services.AddOrganizationsTenancyExtension();",
            "builder.Services.AddBunkFyWorkspaces(options => options.GlobalAuthScopeId = authScopeId);",
            "builder.Services.AddBunkFyWorkspaceAdmission(builder.Configuration, builder.Environment.IsProduction());",
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
    public void Public_api_does_not_expose_the_generic_files_front_door()
    {
        string program = RepositoryPaths.Read("src", "BunkFy.Host.Api", "Program.cs");
        string project = RepositoryPaths.Read("src", "BunkFy.Host.Api", "BunkFy.Host.Api.csproj");

        Assert.DoesNotContain("Gma.Modules.Files.Api", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddModule<FilesModule>()", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Gma.Modules.Files.Api.csproj", project, StringComparison.Ordinal);
        Assert.Contains("builder.AddMinioFileStorage();", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_hosts_allow_only_canonical_json_object_storage()
    {
        string[] settingsPaths =
        [
            "src/BunkFy.Host.Api/appsettings.json",
            "src/BunkFy.Host.AdminApi/appsettings.json",
            "src/BunkFy.Host.AdminCli/appsettings.json",
            "src/BunkFy.Host.Worker/appsettings.json"
        ];

        foreach (string path in settingsPaths)
        {
            using JsonDocument document = JsonDocument.Parse(RepositoryPaths.Read(path.Split('/')));
            string[] allowedContentTypes = document.RootElement
                .GetProperty("FileManagement")
                .GetProperty("AllowedContentTypes")
                .EnumerateArray()
                .Select(value => value.GetString())
                .OfType<string>()
                .ToArray();

            Assert.Equal(["application/json"], allowedContentTypes);
        }
    }

    [Fact]
    public void Raw_email_storage_is_an_explicit_local_development_exception()
    {
        string[] settingsPaths =
        [
            "src/BunkFy.Host.Api/appsettings.Development.json",
            "src/BunkFy.Host.Worker/appsettings.Development.json"
        ];

        foreach (string path in settingsPaths)
        {
            using JsonDocument document = JsonDocument.Parse(RepositoryPaths.Read(path.Split('/')));
            string[] allowedContentTypes = document.RootElement
                .GetProperty("FileManagement")
                .GetProperty("AllowedContentTypes")
                .EnumerateArray()
                .Select(value => value.GetString())
                .OfType<string>()
                .ToArray();

            Assert.Equal(["application/json", "message/rfc822"], allowedContentTypes);
        }
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
        Assert.Contains("builder.AddAdminApiModule<WorkspacesAdminApiModule>();", adminApi, StringComparison.Ordinal);
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
        Assert.Contains("builder.AddAdminModule<WorkspacesAdminCliModule>();", adminCli, StringComparison.Ordinal);
        Assert.Contains("builder.AddAdminModule<TaskRuntimeAdminCliModule>();", adminCli, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_hosts_keep_audit_and_bootstrap_settings_under_their_domain_owners()
    {
        string[] settingsPaths =
        [
            "src/BunkFy.Host.AdminApi/appsettings.json",
            "src/BunkFy.Host.AdminCli/appsettings.json"
        ];

        foreach (string path in settingsPaths)
        {
            using JsonDocument document = JsonDocument.Parse(RepositoryPaths.Read(path.Split('/')));
            JsonElement administration = document.RootElement.GetProperty("Administration");
            JsonElement audit = administration.GetProperty("Audit");
            JsonElement bootstrap = document.RootElement
                .GetProperty("AccessControl")
                .GetProperty("Bootstrap");

            Assert.False(administration.TryGetProperty("Bootstrap", out _));
            Assert.Equal(50, audit.GetProperty("DefaultPageSize").GetInt32());
            Assert.Equal(200, audit.GetProperty("MaxPageSize").GetInt32());
            Assert.Equal(500, audit.GetProperty("DefaultPurgeBatchSize").GetInt32());
            Assert.Equal(2000, audit.GetProperty("MaxPurgeBatchSize").GetInt32());
            Assert.False(bootstrap.GetProperty("AllowWhenAssignmentsExist").GetBoolean());
            Assert.Equal("owner", bootstrap.GetProperty("OwnerRoleName").GetString());
        }
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
    public void Public_api_requires_explicit_production_admission_policy()
    {
        string appsettings = RepositoryPaths.Read("src", "BunkFy.Host.Api", "appsettings.json");
        string development = RepositoryPaths.Read("src", "BunkFy.Host.Api", "appsettings.Development.json");
        string program = RepositoryPaths.Read("src", "BunkFy.Host.Api", "Program.cs");

        Assert.Contains("\"SelfRegistration\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"GlobalScopeId\": \"default\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"PasswordEnabled\": true", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"ExternalEnabled\": true", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"SelfServiceCreationEnabled\": true", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"AccountRegistration\": \"Unspecified\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"WorkspaceCreation\": \"Unspecified\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"AccountRegistration\": \"Public\"", development, StringComparison.Ordinal);
        Assert.Contains("\"WorkspaceCreation\": \"SelfService\"", development, StringComparison.Ordinal);
        Assert.Contains("\"RequireVerifiedEmailForWorkspaceCreation\": false", development, StringComparison.Ordinal);
        Assert.Contains(
            "AddBunkFyWorkspaceAdmission(builder.Configuration, builder.Environment.IsProduction())",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Workspace_membership_marker_is_permission_free_and_operation_access_is_profile_owned()
    {
        string roles = RepositoryPaths.Read(
            "src",
            "Modules",
            "Workspaces",
            "BunkFy.Modules.Workspaces.Contracts",
            "WorkspaceAccessRoles.cs");
        string seeds = RepositoryPaths.Read(
            "src",
            "Modules",
            "Workspaces",
            "BunkFy.Modules.Workspaces.Contracts",
            "WorkspaceAccessProfileSeeds.cs");

        Assert.Contains("MembershipMarkerPermissions { get; } = [];", roles, StringComparison.Ordinal);
        Assert.Contains("LegacyMemberPermissions", roles, StringComparison.Ordinal);
        Assert.Contains("WorkspaceAccessRoles.LegacyMemberPermissions", seeds, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessControlPermissionGrants.OwnerWildcard", seeds, StringComparison.Ordinal);
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
