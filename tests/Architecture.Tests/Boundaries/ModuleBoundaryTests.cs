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
    public void Cross_module_references_use_contract_projects()
    {
        string[] moduleNames = ["Catalog", "Ordering", "Properties", "TaskSamples"];
        string[] offenders = ProjectFile.All()
            .Where(project => project.RepositoryPath.StartsWith("src/Modules/", StringComparison.Ordinal))
            .SelectMany(project =>
            {
                string owningModule = project.RepositoryPath.Split('/')[2];
                return project.ProjectReferences
                    .Where(reference => moduleNames
                        .Where(module => !string.Equals(module, owningModule, StringComparison.Ordinal))
                        .Any(module => reference.Contains($"src\\Modules\\{module}\\", StringComparison.OrdinalIgnoreCase) ||
                                       reference.Contains($"$({ModuleRootProperty(module)})", StringComparison.Ordinal)))
                    .Where(reference => !reference.Contains(".Contracts", StringComparison.Ordinal))
                    .Select(reference => $"{project.RepositoryPath} -> {reference}");
            })
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

    private static string ModuleRootProperty(string moduleName) => moduleName switch
    {
        "Catalog" => "GmaModuleCatalogRoot",
        "Ordering" => "GmaModuleOrderingRoot",
        "Properties" => "GmaModulePropertiesRoot",
        "TaskSamples" => "GmaModuleTaskSamplesRoot",
        _ => throw new ArgumentOutOfRangeException(nameof(moduleName), moduleName, null)
    };
}
