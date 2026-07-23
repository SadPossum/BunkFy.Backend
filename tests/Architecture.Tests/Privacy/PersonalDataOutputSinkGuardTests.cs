namespace Architecture.Tests.Privacy;

using System.Text.RegularExpressions;
using Architecture.Tests.Support;
using BunkFy.DataGovernance;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class PersonalDataOutputSinkGuardTests
{
    private static readonly string[] ExpectedCatalogIds =
    [
        "data-rights.personal-data",
        "guests.personal-data",
        "ingestion.personal-data",
        "inventory.personal-data",
        "operations-notifications.personal-data",
        "properties.personal-data",
        "reservations.personal-data",
        "staff.personal-data",
        "workspaces.personal-data"
    ];

    private static readonly Regex LogInvocation = new(
        @"\.Log(?:Trace|Debug|Information|Warning|Error|Critical)\([\s\S]*?\);",
        RegexOptions.CultureInvariant);

    private static readonly Regex ExceptionDerivedValue = new(
        @"\b(?:exception|ex)\s*\.\s*(?:Message|StackTrace|ToString\s*\()",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoggerMessageInvocation = new(
        @"\bLog[A-Za-z0-9_]+\(\s*logger\s*,[\s\S]*?\);",
        RegexOptions.CultureInvariant);

    private static readonly Regex RawExceptionArgument = new(
        @"(?:^|[,(])\s*(?:exception|ex)\s*(?:,|\))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex MessageTemplateProperty = new(
        @"\{(?<name>[^}:,]+)",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Every_source_catalogue_obeys_the_closed_output_sink_policy()
    {
        PersonalDataCatalogDocument[] catalogues = CataloguePaths()
            .Select(path => PersonalDataCatalogJson.Parse(File.ReadAllBytes(path)))
            .OrderBy(catalogue => catalogue.CatalogId, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedCatalogIds, catalogues.Select(catalogue => catalogue.CatalogId));

        HashSet<PersonalDataSurface> prohibited =
        [
            PersonalDataSurface.Log,
            PersonalDataSurface.Metric,
            PersonalDataSurface.Trace,
            PersonalDataSurface.SupportBundle
        ];
        string[] prohibitedFields = catalogues
            .SelectMany(catalogue => catalogue.Fields.Select(field => new { catalogue.CatalogId, Field = field }))
            .Where(item => item.Field.AllowedSurfaces.Any(prohibited.Contains) ||
                           item.Field.Bindings.Any(binding => prohibited.Contains(binding.Surface)))
            .Select(item => $"{item.CatalogId}:{item.Field.Id}")
            .ToArray();

        Assert.Empty(prohibitedFields);

        string[] unreviewedNotificationFields = catalogues
            .Where(catalogue => catalogue.CatalogId != "operations-notifications.personal-data")
            .SelectMany(catalogue => catalogue.Fields.Select(field => new { catalogue.CatalogId, Field = field }))
            .Where(item => item.Field.AllowedSurfaces.Contains(PersonalDataSurface.Notification) ||
                           item.Field.Bindings.Any(binding => binding.Surface == PersonalDataSurface.Notification))
            .Select(item => $"{item.CatalogId}:{item.Field.Id}")
            .ToArray();

        Assert.Empty(unreviewedNotificationFields);
        PersonalDataCatalogDocument notificationCatalogue = Assert.Single(
            catalogues,
            catalogue => catalogue.CatalogId == "operations-notifications.personal-data");
        Assert.Equal(15, notificationCatalogue.Fields.Length);
        Assert.All(notificationCatalogue.Fields, field =>
        {
            Assert.Contains(PersonalDataSurface.Notification, field.AllowedSurfaces);
            Assert.All(field.Bindings, binding =>
                Assert.Equal(PersonalDataSurface.Notification, binding.Surface));
        });
    }

    [Fact]
    public void Composed_source_logs_only_bounded_templates_and_values()
    {
        List<string> offenders = [];
        foreach (string path in ComposedProductionSourceFiles())
        {
            string source = File.ReadAllText(path);
            foreach (Match match in LogInvocation.Matches(source))
            {
                string invocation = match.Value;
                string arguments = invocation[(invocation.IndexOf('(') + 1)..].TrimStart();
                if (!arguments.StartsWith('"'))
                {
                    offenders.Add($"{RepositoryPaths.ToRepositoryPath(path)} uses a non-literal first logging argument");
                }

                if (ExceptionDerivedValue.IsMatch(invocation))
                {
                    offenders.Add($"{RepositoryPaths.ToRepositoryPath(path)} logs exception-derived text");
                }

                foreach (Match property in MessageTemplateProperty.Matches(invocation))
                {
                    string name = property.Groups["name"].Value.TrimStart('@', '$');
                    if (IsSensitiveLogProperty(name))
                    {
                        offenders.Add(
                            $"{RepositoryPaths.ToRepositoryPath(path)} logs sensitive/high-cardinality property {name}");
                    }
                }
            }

            foreach (Match match in LoggerMessageInvocation.Matches(source))
            {
                if (RawExceptionArgument.IsMatch(match.Value))
                {
                    offenders.Add(
                        $"{RepositoryPaths.ToRepositoryPath(path)} passes a raw exception to a LoggerMessage delegate");
                }
            }
        }

        string[] distinctOffenders = offenders.Distinct(StringComparer.Ordinal).ToArray();
        Assert.True(
            distinctOffenders.Length == 0,
            string.Join(Environment.NewLine, distinctOffenders));
    }

    [Fact]
    public void Product_code_does_not_define_unreviewed_custom_trace_or_metric_instruments()
    {
        string[] roots =
        [
            "src/Adapters",
            "src/Extensions",
            "src/Modules",
            "src/Shared"
        ];
        string[] forbiddenTokens =
        [
            "ActivitySource",
            "StartActivity(",
            "new Meter(",
            ".CreateCounter<",
            ".CreateHistogram<",
            ".CreateObservable"
        ];
        string[] offenders = roots
            .Where(root => Directory.Exists(RepositoryPaths.Resolve(root.Split('/'))))
            .SelectMany(root => RepositoryPaths.EnumerateFiles(root, "*.cs"))
            .Where(path => !RepositoryPaths.ToRepositoryPath(path).Contains("/tests/", StringComparison.Ordinal))
            .Where(path => forbiddenTokens.Any(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal)))
            .Select(RepositoryPaths.ToRepositoryPath)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsSensitiveLogProperty(string name) =>
        (!string.Equals(name, "TraceId", StringComparison.OrdinalIgnoreCase) &&
         name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)) ||
        name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Scope", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("User", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("UserId", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Actor", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("ActorId", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Segments", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Identity", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Payload", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Body", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Reason", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Error", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Message", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> CataloguePaths() =>
        Directory.EnumerateFiles(
                RepositoryPaths.Resolve("src"),
                "personal-data-catalog.v1.json",
                SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path));

    private static IEnumerable<string> ComposedProductionSourceFiles()
    {
        IEnumerable<string> product = Directory.EnumerateFiles(
            RepositoryPaths.Resolve("src"),
            "*.cs",
            SearchOption.AllDirectories);
        IEnumerable<string> framework = Directory.EnumerateFiles(
            RepositoryPaths.Resolve("gma", "framework", "src"),
            "*.cs",
            SearchOption.AllDirectories);
        IEnumerable<string> extensions = Directory.EnumerateFiles(
            RepositoryPaths.Resolve("gma", "extensions", "src"),
            "*.cs",
            SearchOption.AllDirectories);
        IEnumerable<string> modules = Directory.EnumerateDirectories(RepositoryPaths.Resolve("gma", "modules"))
            .Select(path => Path.Combine(path, "src"))
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories));

        return product
            .Concat(framework)
            .Concat(extensions)
            .Concat(modules)
            .Where(path => !IsGeneratedPath(path));
    }

    private static bool IsGeneratedPath(string path) =>
        path.Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");
}
