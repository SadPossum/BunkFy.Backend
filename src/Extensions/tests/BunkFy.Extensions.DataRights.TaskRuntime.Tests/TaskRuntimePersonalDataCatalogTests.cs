namespace BunkFy.Extensions.DataRights.TaskRuntime.Tests;

using System.Reflection;
using BunkFy.DataGovernance;
using Gma.Framework.Tasks.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TaskRuntimePersonalDataCatalogTests
{
    private static readonly PersonalDataCatalogDocument Catalog =
        PersonalDataCatalogJson.Parse(
            File.ReadAllBytes(Path.Combine(
                AppContext.BaseDirectory,
                "DataGovernance",
                "personal-data-catalog.v1.json")));

    [Fact]
    public void Catalog_is_the_persistence_only_operational_copy_surface()
    {
        Assert.Equal("task-runtime.personal-data", Catalog.CatalogId);
        Assert.Equal("task-runtime", Catalog.Module);
        Assert.Equal(
            TaskRuntimeTenantTerminationMetadata.PersonalDataCatalogVersion,
            Catalog.CatalogVersion);
        Assert.Equal(
            TaskRuntimeTenantTerminationMetadata.PersonalDataFieldIds
                .Order(StringComparer.Ordinal),
            Catalog.Fields.Select(field => field.Id)
                .Order(StringComparer.Ordinal));
        Assert.All(Catalog.Fields, field =>
        {
            Assert.Equal(
                [PersonalDataSurface.Persistence],
                field.AllowedSurfaces);
            Assert.Contains(
                PersonalDataBoundary.CrossModule,
                field.AllowedBoundaries);
        });
        Assert.DoesNotContain(
            Catalog.Fields.SelectMany(field => field.AllowedSurfaces),
            surface => surface == PersonalDataSurface.DataRightsExport);
    }

    [Fact]
    public void Every_persistence_binding_resolves_to_the_generic_task_store()
    {
        Assembly assembly = typeof(TaskRun).Assembly;
        foreach (PersonalDataFieldDefinition field in Catalog.Fields)
        {
            foreach (PersonalDataMemberBinding binding in field.Bindings)
            {
                Assert.Equal(assembly.GetName().Name, binding.Assembly);
                Type? selectedType = assembly.GetType(
                    binding.Type,
                    throwOnError: false,
                    ignoreCase: false);
                Assert.NotNull(selectedType);
                PropertyInfo? property = selectedType.GetProperty(
                    binding.Member,
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.NotNull(property);
                Assert.Equal(
                    PersonalDataSurface.Persistence,
                    binding.Surface);
            }
        }
    }

    [Fact]
    public void Checked_in_inventory_matches_deterministic_rendering()
    {
        string expected = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "DataGovernance",
                "personal-data-inventory.v1.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(
            expected,
            PersonalDataInventoryRenderer.RenderMarkdown(Catalog));
    }
}
