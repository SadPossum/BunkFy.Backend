namespace BunkFy.Extensions.DataRights.Organizations.Tests;

using System.Reflection;
using BunkFy.DataGovernance;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsPersonalDataCatalogTests
{
    private static readonly PersonalDataCatalogDocument Catalog =
        PersonalDataCatalogJson.Parse(
            File.ReadAllBytes(Path.Combine(
                AppContext.BaseDirectory,
                "DataGovernance",
                "personal-data-catalog.v1.json")));

    [Fact]
    public void Catalog_is_exactly_the_typed_tenant_export_surface()
    {
        Assert.Equal("organizations.personal-data", Catalog.CatalogId);
        Assert.Equal(
            OrganizationsTenantTerminationMetadata.PersonalDataCatalogVersion,
            Catalog.CatalogVersion);
        Assert.Equal(
            OrganizationsTenantTerminationMetadata.ExportFieldIds
                .Order(StringComparer.Ordinal),
            Catalog.Fields.Select(field => field.Id)
                .Order(StringComparer.Ordinal));
        Assert.All(Catalog.Fields, field =>
        {
            Assert.Equal(
                [PersonalDataSurface.DataRightsExport],
                field.AllowedSurfaces);
            Assert.Contains(
                PersonalDataBoundary.CrossModule,
                field.AllowedBoundaries);
        });
    }

    [Fact]
    public void Every_binding_resolves_and_matches_the_export_attribute()
    {
        Assembly assembly = typeof(OrganizationsTenantTerminationMetadata)
            .Assembly;
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
                Type type = selectedType;
                PropertyInfo? selectedProperty = type.GetProperty(
                    binding.Member,
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.NotNull(selectedProperty);
                PropertyInfo property = selectedProperty;
                OrganizationsTenantExportFieldAttribute attribute =
                    Assert.IsType<OrganizationsTenantExportFieldAttribute>(
                        property.GetCustomAttribute<
                            OrganizationsTenantExportFieldAttribute>());
                Assert.Equal(field.Id, attribute.FieldId);
                Assert.Equal(
                    PersonalDataSurface.DataRightsExport,
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
