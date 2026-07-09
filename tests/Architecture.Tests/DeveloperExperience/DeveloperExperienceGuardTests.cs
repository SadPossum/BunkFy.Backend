namespace Architecture.Tests.DeveloperExperience;

using System.Xml.Linq;
using Architecture.Tests.Support;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class DeveloperExperienceGuardTests
{
    [Fact]
    public void Projects_under_src_and_tests_are_listed_in_solution()
    {
        string solution = RepositoryPaths.Read("BunkFy.slnx");
        string[] missing = ProjectFile.All()
            .Select(project => project.RepositoryPath)
            .Where(path => !solution.Contains(path, StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Operational_files_are_listed_in_solution()
    {
        string solution = RepositoryPaths.Read("BunkFy.slnx");
        string[] expected =
        [
            ".editorconfig",
            ".gitattributes",
            ".github/workflows/validate.yml",
            "Directory.Build.props",
            "Directory.Packages.props",
            ".gitmodules",
            "Gma.SourceRoots.props.example",
            "eng/restore.ps1",
            "eng/build.ps1",
            "eng/test-fast.ps1",
            "eng/test-docker.ps1",
            "eng/verify.ps1",
            "requests/auth.http",
            "requests/admin-api.http"
        ];

        string[] missing = expected
            .Where(path => !solution.Contains(path, StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Source_roots_include_reusable_gma_modules_and_local_examples()
    {
        string sourceRoots = RepositoryPaths.Read("Gma.SourceRoots.props.example");
        string[] expectedTokens =
        [
            "gma\\framework\\src\\",
            "<GmaModuleAuthRoot>$(GmaModulesRoot)auth\\src\\</GmaModuleAuthRoot>",
            "<GmaModuleAdministrationRoot>$(GmaModulesRoot)administration\\src\\</GmaModuleAdministrationRoot>",
            "<GmaModuleFilesRoot>$(GmaModulesRoot)files\\src\\</GmaModuleFilesRoot>",
            "<GmaModuleNotificationsRoot>$(GmaModulesRoot)notifications\\src\\</GmaModuleNotificationsRoot>",
            "<GmaModuleTaskRuntimeRoot>$(GmaModulesRoot)task-runtime\\src\\</GmaModuleTaskRuntimeRoot>",
            "<GmaModuleTenancyRoot>$(GmaModulesRoot)tenancy\\src\\</GmaModuleTenancyRoot>",
            "src\\Modules\\Catalog\\",
            "src\\Modules\\Ordering\\",
            "src\\Modules\\TaskSamples\\"
        ];

        string[] missing = expectedTokens
            .Where(token => !sourceRoots.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Package_versions_are_centralized()
    {
        XDocument packages = XDocument.Load(RepositoryPaths.Resolve("Directory.Packages.props"));
        string[] centralVersions = packages
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        Assert.Contains("Microsoft.NET.Test.Sdk", centralVersions);
        Assert.Contains("Microsoft.EntityFrameworkCore", centralVersions);
        Assert.Contains("Aspire.Hosting.AppHost", centralVersions);

        string[] inlineVersions = ProjectFile.All()
            .SelectMany(project => project.Document
                .Descendants()
                .Where(element => element.Name.LocalName == "PackageReference")
                .Where(element => element.Attribute("Version") is not null)
                .Select(_ => project.RepositoryPath))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(inlineVersions);
    }

    [Fact]
    public void Test_projects_are_marked_and_keep_runner_private()
    {
        string[] failures = ProjectFile.All()
            .Where(project => project.RepositoryPath.Contains("/tests/", StringComparison.Ordinal) ||
                              project.RepositoryPath.StartsWith("tests/", StringComparison.Ordinal))
            .SelectMany(project =>
            {
                List<string> projectFailures = [];
                bool isTestProject = project.Document
                    .Descendants()
                    .Any(element => element.Name.LocalName == "IsTestProject" &&
                                    string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
                bool isPackableFalse = project.Document
                    .Descendants()
                    .Any(element => element.Name.LocalName == "IsPackable" &&
                                    string.Equals(element.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase));
                bool runnerPrivate = project.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "PackageReference" &&
                                      string.Equals(element.Attribute("Include")?.Value, "xunit.runner.visualstudio", StringComparison.Ordinal))
                    .Elements()
                    .Any(element => element.Name.LocalName == "PrivateAssets" &&
                                    string.Equals(element.Value.Trim(), "all", StringComparison.OrdinalIgnoreCase));

                if (!isTestProject)
                {
                    projectFailures.Add($"{project.RepositoryPath} missing IsTestProject=true");
                }

                if (!isPackableFalse)
                {
                    projectFailures.Add($"{project.RepositoryPath} missing IsPackable=false");
                }

                if (!runnerPrivate)
                {
                    projectFailures.Add($"{project.RepositoryPath} must keep xunit.runner.visualstudio private");
                }

                return projectFailures;
            })
            .ToArray();

        Assert.Empty(failures);
    }
}
