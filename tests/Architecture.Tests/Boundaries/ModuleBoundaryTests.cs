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
    public void Data_governance_catalogue_model_stays_dependency_free()
    {
        ProjectFile governance = Assert.Single(
            ProjectFile.All(),
            project => string.Equals(project.Name, "BunkFy.DataGovernance", StringComparison.Ordinal));

        Assert.Empty(governance.PackageReferences);
        Assert.Empty(governance.ProjectReferences);
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
        string absolute = Path.GetFullPath(reference, projectDirectory);
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
