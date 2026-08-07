namespace Architecture.Tests.Boundaries;

using Architecture.Tests.Support;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Module_domain_projects_do_not_depend_on_application_contracts_or_adapters()
    {
        string[] offenders = ModuleProjects(".Domain")
            .Where(project => project.ProjectReferences.Any(IsNonDomainModuleReference))
            .Select(project => project.RepositoryPath)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_application_projects_do_not_depend_on_front_doors_or_persistence()
    {
        string[] forbiddenTokens =
        [
            ".Api\\",
            ".AdminApi\\",
            ".AdminCli\\",
            ".Persistence\\",
            ".Persistence.",
            "Host."
        ];

        string[] offenders = ModuleProjects(".Application")
            .Where(project => project.ProjectReferences.Any(reference =>
                forbiddenTokens.Any(token => reference.Contains(token, StringComparison.OrdinalIgnoreCase))))
            .Select(project => project.RepositoryPath)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_contract_projects_stay_backend_free()
    {
        string[] forbiddenPackages =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore.Authentication.JwtBearer",
            "NATS.Net",
            "Serilog.AspNetCore",
            "Minio"
        ];

        string[] offenders = ProjectFile.All()
            .Where(project => project.RepositoryPath.StartsWith("src/Modules/", StringComparison.Ordinal) &&
                              project.Name.EndsWith(".Contracts", StringComparison.Ordinal))
            .Where(project => project.ProjectReferences.Any(reference =>
                              reference.Contains(".Application", StringComparison.OrdinalIgnoreCase) ||
                              reference.Contains(".Domain", StringComparison.OrdinalIgnoreCase) ||
                              reference.Contains(".Persistence", StringComparison.OrdinalIgnoreCase) ||
                              reference.Contains(".Api", StringComparison.OrdinalIgnoreCase)) ||
                              project.PackageReferences.Any(package =>
                                  forbiddenPackages.Contains(package, StringComparer.Ordinal)))
            .Select(project => project.RepositoryPath)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Product_code_outside_a_module_uses_its_contract_projects()
    {
        string[] offenders = ProjectFile.All()
            .Where(project => project.RepositoryPath.StartsWith("src/", StringComparison.Ordinal))
            .Where(project => !IsCompositionHost(project.RepositoryPath))
            .SelectMany(project => project.ProjectReferences.Select(reference => new
            {
                Project = project,
                Reference = reference,
                Target = ResolveProjectReference(project, reference)
            }))
            .Where(item => item.Target is not null &&
                item.Target.StartsWith("src/Modules/", StringComparison.Ordinal))
            .Where(item => !BelongsToSameModule(item.Project.RepositoryPath, item.Target!))
            .Where(item => !IsContractProject(item.Target!))
            .Select(item => $"{item.Project.RepositoryPath} -> {item.Target}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Project_reference_resolution_is_platform_neutral()
    {
        ProjectFile extension = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Extensions.DataRights.TenantTermination",
                StringComparison.Ordinal));
        string reference = Assert.Single(
            extension.ProjectReferences,
            candidate => candidate.EndsWith(
                "BunkFy.Modules.DataRights.Contracts\\BunkFy.Modules.DataRights.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            "src/Modules/DataRights/BunkFy.Modules.DataRights.Contracts/BunkFy.Modules.DataRights.Contracts.csproj",
            ResolveProjectReference(extension, reference));
    }

    [Fact]
    public void Product_extensions_use_access_control_through_its_contracts_facade()
    {
        string[] projectReferenceOffenders = ProjectFile.All()
            .Where(project => project.RepositoryPath.StartsWith("src/Extensions/", StringComparison.Ordinal))
            .SelectMany(project => project.ProjectReferences
                .Where(reference => reference.Contains(
                    "Gma.Modules.AccessControl.",
                    StringComparison.OrdinalIgnoreCase))
                .Where(reference => !reference.Contains(
                    "Gma.Modules.AccessControl.Contracts",
                    StringComparison.OrdinalIgnoreCase))
                .Select(reference => $"{project.RepositoryPath} -> {reference}"))
            .ToArray();

        string[] sourceOffenders = RepositoryPaths.EnumerateFiles("src/Extensions", "*.cs")
            .Where(path => File.ReadAllText(path).Contains(
                "Gma.Modules.AccessControl.Application",
                StringComparison.Ordinal))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();

        Assert.Empty(projectReferenceOffenders.Concat(sourceOffenders));
    }

    [Fact]
    public void Reservation_guest_record_extension_uses_only_module_contracts()
    {
        ProjectFile extension = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Extensions.ReservationGuestRecords",
                StringComparison.Ordinal));
        string[] moduleReferences = extension.ProjectReferences
            .Where(reference => reference.Contains(
                "BunkFy.Modules.",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(2, moduleReferences.Length);
        Assert.All(moduleReferences, reference => Assert.Contains(
            ".Contracts\\",
            reference,
            StringComparison.OrdinalIgnoreCase));

        string[] forbiddenSourceReferences = RepositoryPaths.EnumerateFiles(
                "src/Extensions/BunkFy.Extensions.ReservationGuestRecords",
                "*.cs")
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains(
                        "BunkFy.Modules.Guests.Application",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "BunkFy.Modules.Guests.Domain",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "BunkFy.Modules.Guests.Persistence",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "BunkFy.Modules.Reservations.Application",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "BunkFy.Modules.Reservations.Domain",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "BunkFy.Modules.Reservations.Persistence",
                        StringComparison.Ordinal);
            })
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();

        Assert.Empty(forbiddenSourceReferences);
    }

    [Fact]
    public void Operations_notifications_data_rights_adapter_uses_the_generic_notifications_application_boundary()
    {
        ProjectFile extension = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Extensions.Operations.Notifications",
                StringComparison.Ordinal));

        Assert.Contains(
            extension.ProjectReferences,
            reference => reference.EndsWith(
                "Gma.Modules.Notifications.Application\\Gma.Modules.Notifications.Application.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            extension.ProjectReferences,
            reference => reference.EndsWith(
                "Gma.Modules.Notifications.Contracts\\Gma.Modules.Notifications.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            extension.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.Modules.DataRights.Contracts\\BunkFy.Modules.DataRights.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            extension.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.Modules.Ingestion.Contracts\\BunkFy.Modules.Ingestion.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            extension.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.DataGovernance\\BunkFy.DataGovernance.csproj",
                StringComparison.OrdinalIgnoreCase));

        string[] forbiddenProjectReferences = extension.ProjectReferences
            .Where(reference =>
                reference.Contains(
                    "Gma.Modules.Notifications.Domain",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "Gma.Modules.Notifications.Persistence",
                    StringComparison.OrdinalIgnoreCase) ||
                (reference.Contains(
                     "BunkFy.Modules.Ingestion.",
                     StringComparison.OrdinalIgnoreCase) &&
                 !reference.Contains(
                     "BunkFy.Modules.Ingestion.Contracts",
                     StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        string[] forbiddenSourceReferences = RepositoryPaths.EnumerateFiles(
                "src/Extensions/BunkFy.Extensions.Operations.Notifications",
                "*.cs")
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains(
                        "Gma.Modules.Notifications.Domain",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "Gma.Modules.Notifications.Persistence",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "NotificationsDbContext",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "BunkFy.Modules.Ingestion.Application",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "BunkFy.Modules.Ingestion.Domain",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "BunkFy.Modules.Ingestion.Persistence",
                        StringComparison.Ordinal);
            })
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();

        Assert.Empty(forbiddenProjectReferences);
        Assert.Empty(forbiddenSourceReferences);
    }

    [Fact]
    public void Standalone_adapter_runtime_stays_dependency_light()
    {
        ProjectFile runtime = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(project.Name, "BunkFy.Adapter.Runtime", StringComparison.Ordinal));

        Assert.Empty(runtime.PackageReferences);
        string reference = Assert.Single(runtime.ProjectReferences);
        Assert.EndsWith(
            "BunkFy.Adapter.Abstractions\\BunkFy.Adapter.Abstractions.csproj",
            reference,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Data_governance_engine_stays_dependency_free()
    {
        ProjectFile governance = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(project.Name, "BunkFy.DataGovernance", StringComparison.Ordinal));

        Assert.Empty(governance.PackageReferences);
        Assert.Empty(governance.ProjectReferences);
    }

    [Fact]
    public void BunkFy_country_policy_model_does_not_leak_into_GMA()
    {
        string[] productSpecificTokens =
        [
            "CountryPolicyPackDocument",
            "CountryPolicyRegistry",
            "OperatingCountryCode",
            "JurisdictionPolicyId",
            "TransferProfileId"
        ];

        string[] offenders = RepositoryPaths.EnumerateFiles("gma", "*.cs")
            .Where(path => productSpecificTokens.Any(token =>
                File.ReadAllText(path).Contains(token, StringComparison.Ordinal)))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void BunkFy_data_rights_model_does_not_leak_into_GMA()
    {
        string[] productSpecificTokens =
        [
            "DataRightsCase",
            "DataRightsOperation",
            "DataRightsRestriction",
            "DataRightsRequesterRelationship",
            "DataRightsAdminPermissionCodes",
            "ITenantTerminationContributor",
            "TenantTerminationContribution",
            "TenantTerminationProcess"
        ];

        string[] offenders = RepositoryPaths.EnumerateFiles("gma", "*.cs")
            .Where(path => productSpecificTokens.Any(token =>
                File.ReadAllText(path).Contains(token, StringComparison.Ordinal)))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Data_rights_persistence_does_not_depend_on_workspaces()
    {
        ProjectFile persistence = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Modules.DataRights.Persistence",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            persistence.ProjectReferences,
            reference => reference.Contains(
                "BunkFy.Modules.Workspaces",
                StringComparison.OrdinalIgnoreCase));

        string[] sourceOffenders = RepositoryPaths.EnumerateFiles(
                "src/Modules/DataRights/BunkFy.Modules.DataRights.Persistence",
                "*.cs")
            .Where(path => File.ReadAllText(path).Contains(
                "BunkFy.Modules.Workspaces",
                StringComparison.Ordinal))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();
        Assert.Empty(sourceOffenders);
    }

    [Fact]
    public void Data_rights_companion_selection_stays_owner_generic_and_contract_coupled()
    {
        string[] dataRightsProjects =
        [
            "BunkFy.Modules.DataRights.Contracts",
            "BunkFy.Modules.DataRights.Application"
        ];
        foreach (string projectName in dataRightsProjects)
        {
            ProjectFile project = Assert.Single(
                ProjectFile.All(),
                candidate => string.Equals(
                    candidate.Name,
                    projectName,
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                project.ProjectReferences,
                reference =>
                    reference.Contains(
                        "BunkFy.Modules.Workspaces",
                        StringComparison.OrdinalIgnoreCase) ||
                    reference.Contains(
                        "BunkFy.Modules.Staff",
                        StringComparison.OrdinalIgnoreCase));
        }

        string[] dataRightsSourceOffenders =
            RepositoryPaths.EnumerateFiles(
                    "src/Modules/DataRights/BunkFy.Modules.DataRights.Contracts",
                    "*.cs")
                .Concat(RepositoryPaths.EnumerateFiles(
                    "src/Modules/DataRights/BunkFy.Modules.DataRights.Application",
                    "*.cs"))
                .Where(path =>
                {
                    string source = File.ReadAllText(path);
                    return source.Contains(
                            "BunkFy.Modules.Workspaces",
                            StringComparison.Ordinal) ||
                        source.Contains(
                            "BunkFy.Modules.Staff",
                            StringComparison.Ordinal) ||
                        source.Contains(
                            "WorkspaceStaff",
                            StringComparison.Ordinal);
                })
                .Select(RepositoryPaths.ToRepositoryPath)
                .ToArray();
        Assert.Empty(dataRightsSourceOffenders);

        ProjectFile workspacesApplication = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Modules.Workspaces.Application",
                StringComparison.Ordinal));
        Assert.Contains(
            workspacesApplication.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.Modules.DataRights.Contracts\\BunkFy.Modules.DataRights.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            workspacesApplication.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.Modules.Staff.Contracts\\BunkFy.Modules.Staff.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            workspacesApplication.ProjectReferences,
            reference =>
                reference.Contains(
                    "BunkFy.Modules.DataRights.Application",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.DataRights.Domain",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.DataRights.Persistence",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Staff.Application",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Staff.Domain",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Staff.Persistence",
                    StringComparison.OrdinalIgnoreCase));

        string[] gmaOffenders = RepositoryPaths.EnumerateFiles(
                "gma",
                "*.cs")
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains(
                        "DataRightsRequiredCompanion",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "WorkspaceStaffCorrelationAnonymisation",
                        StringComparison.Ordinal);
            })
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();
        Assert.Empty(gmaOffenders);
    }

    [Fact]
    public void Ingestion_anonymisation_stays_product_owned_and_off_front_doors()
    {
        string dependencyInjection = RepositoryPaths.Read(
            "src",
            "Modules",
            "Ingestion",
            "BunkFy.Modules.Ingestion.Application",
            "DependencyInjection.cs");
        Assert.Contains(
            "IDataRightsAnonymisationRestoreContributor",
            dependencyInjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "IDataRightsAnonymisationContributor",
            dependencyInjection,
            StringComparison.Ordinal);

        string[] frontDoorOffenders =
            RepositoryPaths.EnumerateFiles(
                "src/Modules/Ingestion/BunkFy.Modules.Ingestion.Api",
                "*.cs")
            .Concat(RepositoryPaths.EnumerateFiles(
                "src/Modules/Ingestion/BunkFy.Modules.Ingestion.AdminApi",
                "*.cs"))
            .Concat(RepositoryPaths.EnumerateFiles(
                "src/Modules/Ingestion/BunkFy.Modules.Ingestion.AdminCli",
                "*.cs"))
            .Where(path => File.ReadAllText(path).Contains(
                "Anonymisation",
                StringComparison.Ordinal))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();
        Assert.Empty(frontDoorOffenders);

        string[] gmaOffenders = RepositoryPaths.EnumerateFiles(
                "gma",
                "*.cs")
            .Where(path => File.ReadAllText(path).Contains(
                "IngestionAnonymisation",
                StringComparison.Ordinal))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();
        Assert.Empty(gmaOffenders);
    }

    [Fact]
    public void Guest_retention_stays_product_owned_and_contract_coupled()
    {
        ProjectFile guestsApplication = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Modules.Guests.Application",
                StringComparison.Ordinal));
        Assert.Contains(
            guestsApplication.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.Modules.Retention.Contracts\\BunkFy.Modules.Retention.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            guestsApplication.ProjectReferences,
            reference =>
                reference.Contains(
                    "BunkFy.Modules.Retention.Application",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Retention.Domain",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Retention.Persistence",
                    StringComparison.OrdinalIgnoreCase));

        string[] retentionImplementationOffenders =
            RepositoryPaths.EnumerateFiles(
                    "src/Modules/Retention",
                    "*.cs")
                .Where(path => File.ReadAllText(path).Contains(
                    "BunkFy.Modules.Guests.",
                    StringComparison.Ordinal))
                .Select(RepositoryPaths.ToRepositoryPath)
                .ToArray();
        Assert.Empty(retentionImplementationOffenders);

        string[] gmaOffenders = RepositoryPaths.EnumerateFiles(
                "gma",
                "*.cs")
            .Where(path => File.ReadAllText(path).Contains(
                "GuestRetention",
                StringComparison.Ordinal))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();
        Assert.Empty(gmaOffenders);
    }

    [Fact]
    public void Reservation_retention_stays_product_owned_and_contract_coupled()
    {
        ProjectFile reservationsApplication = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Modules.Reservations.Application",
                StringComparison.Ordinal));
        Assert.Contains(
            reservationsApplication.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.Modules.Retention.Contracts\\BunkFy.Modules.Retention.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            reservationsApplication.ProjectReferences,
            reference =>
                reference.Contains(
                    "BunkFy.Modules.Retention.Application",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Retention.Domain",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Retention.Persistence",
                    StringComparison.OrdinalIgnoreCase));

        string[] retentionImplementationOffenders =
            RepositoryPaths.EnumerateFiles(
                    "src/Modules/Retention",
                    "*.cs")
                .Where(path => File.ReadAllText(path).Contains(
                    "BunkFy.Modules.Reservations.",
                    StringComparison.Ordinal))
                .Select(RepositoryPaths.ToRepositoryPath)
                .ToArray();
        Assert.Empty(retentionImplementationOffenders);

        string[] gmaOffenders = RepositoryPaths.EnumerateFiles(
                "gma",
                "*.cs")
            .Where(path => File.ReadAllText(path).Contains(
                "ReservationRetention",
                StringComparison.Ordinal))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();
        Assert.Empty(gmaOffenders);
    }

    [Fact]
    public void Staff_retention_stays_product_owned_and_contract_coupled()
    {
        ProjectFile staffApplication = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Modules.Staff.Application",
                StringComparison.Ordinal));
        Assert.Contains(
            staffApplication.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.Modules.Retention.Contracts\\BunkFy.Modules.Retention.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            staffApplication.ProjectReferences,
            reference =>
                reference.Contains(
                    "BunkFy.Modules.Retention.Application",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Retention.Domain",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Retention.Persistence",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Workspaces.",
                    StringComparison.OrdinalIgnoreCase));

        ProjectFile workspacesApplication = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Modules.Workspaces.Application",
                StringComparison.Ordinal));
        Assert.Contains(
            workspacesApplication.ProjectReferences,
            reference => reference.EndsWith(
                "BunkFy.Modules.Staff.Contracts\\BunkFy.Modules.Staff.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            workspacesApplication.ProjectReferences,
            reference =>
                reference.Contains(
                    "BunkFy.Modules.Staff.Application",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Staff.Domain",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Staff.Persistence",
                    StringComparison.OrdinalIgnoreCase));

        string[] retentionImplementationOffenders =
            RepositoryPaths.EnumerateFiles(
                    "src/Modules/Retention",
                    "*.cs")
                .Where(path => File.ReadAllText(path).Contains(
                    "BunkFy.Modules.Staff.",
                    StringComparison.Ordinal))
                .Select(RepositoryPaths.ToRepositoryPath)
                .ToArray();
        Assert.Empty(retentionImplementationOffenders);

        string[] gmaOffenders = RepositoryPaths.EnumerateFiles(
                "gma",
                "*.cs")
            .Where(path => File.ReadAllText(path).Contains(
                "StaffRetention",
                StringComparison.Ordinal))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();
        Assert.Empty(gmaOffenders);
    }

    [Fact]
    public void Remote_adapter_lease_protocol_stays_in_shared_runtime_transport_and_ingestion()
    {
        string[] allowedRoots =
        [
            "src/Shared/BunkFy.Adapter.Abstractions/",
            "src/Shared/BunkFy.Adapter.Runtime/",
            "src/Adapters/",
            "src/BunkFy.AdapterHost/",
            "src/Modules/Ingestion/"
        ];
        string[] protocolTokens =
        [
            "AdapterRemoteLease",
            "IAdapterRemoteControlClient",
            "RemoteLeasedAdapter"
        ];

        string[] offenders = RepositoryPaths.EnumerateFiles("src", "*.cs")
            .Concat(RepositoryPaths.EnumerateFiles("gma", "*.cs"))
            .Select(path => new
            {
                Path = RepositoryPaths.ToRepositoryPath(path),
                Content = File.ReadAllText(path)
            })
            .Where(file => !allowedRoots.Any(root => file.Path.StartsWith(root, StringComparison.Ordinal)))
            .Where(file => protocolTokens.Any(token => file.Content.Contains(token, StringComparison.Ordinal)))
            .Select(file => file.Path)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Workspace_termination_composition_uses_only_explicit_contract_dependencies()
    {
        ProjectFile workspacesApplication = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Modules.Workspaces.Application",
                StringComparison.Ordinal));
        string[] requiredContracts =
        [
            "BunkFy.Modules.DataRights.Contracts\\BunkFy.Modules.DataRights.Contracts.csproj",
            "BunkFy.Modules.Ingestion.Contracts\\BunkFy.Modules.Ingestion.Contracts.csproj",
            "BunkFy.Modules.Properties.Contracts\\BunkFy.Modules.Properties.Contracts.csproj"
        ];

        Assert.All(requiredContracts, required =>
            Assert.Contains(
                workspacesApplication.ProjectReferences,
                reference => reference.EndsWith(
                    required,
                    StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            workspacesApplication.ProjectReferences,
            reference =>
                reference.Contains(
                    "BunkFy.Modules.DataRights.Application",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Ingestion.Application",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(
                    "BunkFy.Modules.Properties.Application",
                    StringComparison.OrdinalIgnoreCase));

        string[] gmaOffenders = RepositoryPaths.EnumerateFiles("gma", "*.cs")
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains(
                        "WorkspaceTermination",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "TenantTermination",
                        StringComparison.Ordinal);
            })
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();
        Assert.Empty(gmaOffenders);
    }

    [Fact]
    public void Independently_authenticated_adapter_routes_cannot_bypass_lifecycle_admission()
    {
        string api = RepositoryPaths.Read(
            "src/Modules/Ingestion/BunkFy.Modules.Ingestion.Api/IngestionModule.cs");
        string receiveHandler = RepositoryPaths.Read(
            "src/Modules/Ingestion/BunkFy.Modules.Ingestion.Application/Handlers/ReceiveObservationCommandHandler.cs");
        string gate = RepositoryPaths.Read(
            "src/Modules/Ingestion/BunkFy.Modules.Ingestion.Application/Ingress/AdapterIngressGate.cs");

        Assert.Equal(
            5,
            CountOccurrences(
                api,
                ".RequireTenantWithIndependentAuthentication()"));
        Assert.Equal(
            4,
            CountOccurrences(api, "AdmitRemoteControlAsync("));
        Assert.Equal(
            2,
            CountOccurrences(api, "new ReceiveObservationCommand("));
        Assert.Contains(
            "ingressGate.AdmitAsync(",
            receiveHandler,
            StringComparison.Ordinal);
        Assert.Contains(
            "IngestionTenantLifecycleAdmission.EvaluateAsync(",
            gate,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MailKit_is_confined_to_the_imap_adapter_package()
    {
        ProjectFile adapter = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(
                project.Name,
                "BunkFy.Adapters.ImapReservationMail",
                StringComparison.Ordinal));

        Assert.Equal(
            ["MailKit", "Microsoft.Extensions.DependencyInjection.Abstractions"],
            adapter.PackageReferences.Order(StringComparer.Ordinal).ToArray());
        Assert.Contains(adapter.ProjectReferences, reference => reference.EndsWith(
            "BunkFy.Adapter.Abstractions\\BunkFy.Adapter.Abstractions.csproj",
            StringComparison.OrdinalIgnoreCase));
        Assert.Contains(adapter.ProjectReferences, reference => reference.EndsWith(
            "BunkFy.Parsers.ReservationMail\\BunkFy.Parsers.ReservationMail.csproj",
            StringComparison.OrdinalIgnoreCase));

        string[] otherMailKitOwners = ProjectFile.All()
            .Where(project => !string.Equals(project.Name, adapter.Name, StringComparison.Ordinal))
            .Where(project => project.PackageReferences.Contains("MailKit", StringComparer.Ordinal))
            .Select(project => project.RepositoryPath)
            .ToArray();
        Assert.Empty(otherMailKitOwners);
    }

    [Fact]
    public void MimeKit_is_confined_to_the_reservation_mail_parser_package()
    {
        ProjectFile parser = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(project.Name, "BunkFy.Parsers.ReservationMail", StringComparison.Ordinal));

        Assert.Equal(
            ["Microsoft.Extensions.DependencyInjection.Abstractions", "MimeKit"],
            parser.PackageReferences.Order(StringComparer.Ordinal).ToArray());
        Assert.Contains(parser.ProjectReferences, reference => reference.EndsWith(
            "BunkFy.Adapter.Abstractions\\BunkFy.Adapter.Abstractions.csproj",
            StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parser.ProjectReferences, reference => reference.EndsWith(
            "BunkFy.Ingestion.Parsing.Abstractions\\BunkFy.Ingestion.Parsing.Abstractions.csproj",
            StringComparison.OrdinalIgnoreCase));

        string[] otherOwners = ProjectFile.All()
            .Where(project => !string.Equals(project.Name, parser.Name, StringComparison.Ordinal))
            .Where(project => project.PackageReferences.Contains("MimeKit", StringComparer.Ordinal))
            .Select(project => project.RepositoryPath)
            .ToArray();
        Assert.Empty(otherOwners);
    }

    [Fact]
    public void Reservation_mail_authentication_protocol_stays_out_of_product_modules_and_hosts()
    {
        string[] allowedRoots =
        [
            "src/Adapters/BunkFy.Adapters.ImapReservationMail/",
            "src/Adapters/BunkFy.Parsers.ReservationMail/"
        ];
        string[] protocolTokens =
        [
            "X-BunkFy-Attachment-Signature",
            "BunkFy.ImapReservationMail.Attachment.v2"
        ];

        string[] offenders = RepositoryPaths.EnumerateFiles("src", "*.cs")
            .Select(path => new
            {
                Path = RepositoryPaths.ToRepositoryPath(path),
                Content = File.ReadAllText(path)
            })
            .Where(file => !allowedRoots.Any(root => file.Path.StartsWith(root, StringComparison.Ordinal)))
            .Where(file => protocolTokens.Any(token => file.Content.Contains(token, StringComparison.Ordinal)))
            .Select(file => file.Path)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static IEnumerable<ProjectFile> ModuleProjects(string suffix) =>
        ProjectFile.All()
            .Where(project => project.RepositoryPath.StartsWith("src/Modules/", StringComparison.Ordinal) &&
            project.Name.EndsWith(suffix, StringComparison.Ordinal));

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static bool IsNonDomainModuleReference(string reference) =>
        reference.Contains(".Application", StringComparison.OrdinalIgnoreCase) ||
        reference.Contains(".Contracts", StringComparison.OrdinalIgnoreCase) ||
        reference.Contains(".Persistence", StringComparison.OrdinalIgnoreCase) ||
        reference.Contains(".Api", StringComparison.OrdinalIgnoreCase) ||
        reference.Contains(".Admin", StringComparison.OrdinalIgnoreCase) ||
        reference.Contains("Host.", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompositionHost(string repositoryPath) =>
        repositoryPath.StartsWith("src/BunkFy.Host.", StringComparison.Ordinal) ||
        repositoryPath.StartsWith("src/BunkFy.AdapterHost/", StringComparison.Ordinal);

    private static string? ResolveProjectReference(ProjectFile project, string reference)
    {
        if (reference.Contains("$(", StringComparison.Ordinal))
        {
            return null;
        }

        string projectDirectory = Path.GetDirectoryName(project.Path)!;
        string platformReference = reference
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        string absolute = Path.GetFullPath(platformReference, projectDirectory);
        return RepositoryPaths.ToRepositoryPath(absolute);
    }

    private static bool BelongsToSameModule(string source, string target)
    {
        string? sourceModule = ModuleName(source);
        string? targetModule = ModuleName(target);
        return sourceModule is not null && string.Equals(sourceModule, targetModule, StringComparison.Ordinal);
    }

    private static string? ModuleName(string repositoryPath)
    {
        string[] segments = repositoryPath.Split('/');
        return segments.Length > 2 && segments[0] == "src" && segments[1] == "Modules"
            ? segments[2]
            : null;
    }

    private static bool IsContractProject(string repositoryPath)
    {
        string name = Path.GetFileNameWithoutExtension(repositoryPath);
        return name.EndsWith(".Contracts", StringComparison.Ordinal);
    }
}
