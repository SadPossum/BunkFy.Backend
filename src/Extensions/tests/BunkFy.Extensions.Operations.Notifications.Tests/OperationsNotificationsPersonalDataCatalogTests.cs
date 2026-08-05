namespace BunkFy.Extensions.Operations.Notifications.Tests;

using System.Reflection;
using BunkFy.DataGovernance;
using Gma.Modules.Notifications.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OperationsNotificationsPersonalDataCatalogTests
{
    private static readonly PersonalDataCatalogDocument Catalogue = LoadCatalogue();
    private static readonly Assembly ExtensionAssembly = typeof(DependencyInjection).Assembly;
    private static readonly Dictionary<string, Assembly> Assemblies = new[]
    {
        ExtensionAssembly,
        typeof(UserNotificationRequestedIntegrationEventV3).Assembly
    }.ToDictionary(assembly => assembly.GetName().Name!, StringComparer.Ordinal);

    [Fact]
    public void Catalogue_version_includes_ingestion_source_link_history()
    {
        Assert.Equal(8, Catalogue.CatalogVersion);
        PersonalDataFieldDefinition references = Assert.Single(
            Catalogue.Fields,
            field => field.Id ==
                "operations-notifications.history-references");
        Assert.Contains("ingestion", references.Sources);
        Assert.Contains(
            "source-link-rights-lifecycle",
            references.Purposes);
    }

    [Fact]
    public void Every_catalogue_binding_resolves_to_a_real_member()
    {
        foreach (PersonalDataMemberBinding binding in Bindings())
        {
            Assert.True(Assemblies.TryGetValue(binding.Assembly, out Assembly? assembly));
            Type? type = assembly.GetType(binding.Type, throwOnError: false, ignoreCase: false);
            Assert.NotNull(type);
            Assert.NotNull(type.GetProperty(binding.Member, BindingFlags.Instance | BindingFlags.Public));
        }
    }

    [Fact]
    public void Every_typed_navigation_payload_member_is_classified_as_notification_data()
    {
        Type[] payloadTypes = ExtensionAssembly.GetTypes()
            .Where(type => typeof(IOperationalNotificationPayload).IsAssignableFrom(type) &&
                           type is { IsInterface: false, IsAbstract: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(8, payloadTypes.Length);
        foreach (Type payloadType in payloadTypes)
        {
            Assert.True(payloadType.IsSealed, $"Payload '{payloadType.FullName}' must be sealed.");
            foreach (PropertyInfo property in payloadType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.NotEqual(typeof(string), property.PropertyType);
                Assert.NotEqual(typeof(object), property.PropertyType);
                AssertBinding(payloadType, property.Name, PersonalDataSurface.Notification);
            }
        }
    }

    [Fact]
    public void Addressed_envelope_personal_members_are_classified()
    {
        foreach (string member in new[]
                 {
                     "EventId",
                     "OccurredAtUtc",
                     "ScopeId",
                     "UserId",
                     "Body",
                     "PayloadJson",
                     "References"
                 })
        {
            AssertBinding(
                typeof(UserNotificationRequestedIntegrationEventV3),
                member,
                PersonalDataSurface.Notification);
        }
    }

    [Fact]
    public void Notification_surface_contains_no_direct_identity_contact_preference_or_free_text_fields()
    {
        HashSet<PersonalDataClassification> prohibited =
        [
            PersonalDataClassification.DirectIdentifier,
            PersonalDataClassification.Contact,
            PersonalDataClassification.Demographic,
            PersonalDataClassification.Preference,
            PersonalDataClassification.FreeText,
            PersonalDataClassification.SearchInput
        ];

        PersonalDataFieldDefinition[] notificationFields = Catalogue.Fields
            .Where(field => field.AllowedSurfaces.Contains(
                PersonalDataSurface.Notification))
            .ToArray();
        Assert.DoesNotContain(
            notificationFields,
            field => prohibited.Contains(field.Classification));
        PersonalDataFieldDefinition payload = Assert.Single(
            Catalogue.Fields,
            field => field.Id ==
                "operations-notifications.payload-envelope");
        Assert.Equal("operations-notifications.payload-envelope", payload.Id);
        Assert.Equal("PayloadJson", Assert.Single(payload.Bindings).Member);
    }

    [Fact]
    public void Guest_history_export_schema_is_catalogue_complete()
    {
        OperationsNotificationsDataRightsExportSchema.EnsureValid();

        PropertyInfo[] properties =
            typeof(ReservationNotificationHistoryDataRightsExport)
                .GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public);
        Assert.Equal(13, properties.Length);
        Assert.Equal(
            properties.Length,
            OperationsNotificationsDataRightsExportSchema
                .Descriptor.FieldIds.Count);
        foreach (PropertyInfo property in properties)
        {
            AssertBinding(
                typeof(ReservationNotificationHistoryDataRightsExport),
                property.Name,
                PersonalDataSurface.DataRightsExport);
        }
    }

    [Fact]
    public void Staff_history_export_schema_is_catalogue_complete()
    {
        OperationsNotificationsStaffDataRightsExportSchema.EnsureValid();

        PropertyInfo[] properties =
            typeof(StaffNotificationHistoryDataRightsExport)
                .GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public);
        Assert.Equal(13, properties.Length);
        Assert.Equal(
            properties.Length,
            OperationsNotificationsStaffDataRightsExportSchema
                .Descriptor.FieldIds.Count);
        foreach (PropertyInfo property in properties)
        {
            AssertBinding(
                typeof(StaffNotificationHistoryDataRightsExport),
                property.Name,
                PersonalDataSurface.DataRightsExport);
        }
    }

    [Fact]
    public void Tenant_scope_export_schema_is_catalogue_complete()
    {
        OperationsNotificationsTenantTerminationExportSchema.EnsureValid();

        Type[] exportTypes =
        [
            typeof(OperationsNotificationTenantExport),
            typeof(OperationsNotificationPreferenceTenantExport),
            typeof(OperationsNotificationDeliveryRouteTenantExport),
            typeof(OperationsNotificationTagDefinitionTenantExport),
            typeof(OperationsNotificationDeliveryTenantExport),
            typeof(OperationsNotificationDeliveryAttemptTenantExport),
            typeof(OperationsNotificationBroadcastTenantExport),
            typeof(OperationsNotificationBroadcastReadTenantExport),
            typeof(OperationsNotificationHistoryReferenceStateTenantExport),
            typeof(OperationsNotificationHistoryCloseReceiptTenantExport),
            typeof(
                OperationsNotificationHistoryBatchCloseOperationTenantExport),
            typeof(
                OperationsNotificationHistoryBatchCloseReceiptTenantExport)
        ];
        PropertyInfo[] properties = exportTypes
            .SelectMany(type => type.GetProperties(
                BindingFlags.Instance |
                BindingFlags.Public))
            .ToArray();
        Assert.Equal(49, properties.Length);
        Assert.Equal(
            37,
            OperationsNotificationsTenantTerminationExportSchema
                .Descriptor.FieldIds.Count);
        Assert.Equal(
            OperationsNotificationsTenantTerminationMetadata.ExportFieldIds
                .Order(StringComparer.Ordinal),
            OperationsNotificationsTenantTerminationExportSchema
                .Descriptor.FieldIds);
        foreach (Type exportType in exportTypes)
        {
            foreach (PropertyInfo property in exportType.GetProperties(
                BindingFlags.Instance |
                BindingFlags.Public))
            {
                AssertBinding(
                    exportType,
                    property.Name,
                    PersonalDataSurface.DataRightsExport);
            }
        }
    }

    [Fact]
    public void Generated_inventory_is_current()
    {
        string expected = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "DataGovernance",
            "personal-data-inventory.v1.md"));

        Assert.Equal(expected, PersonalDataInventoryRenderer.RenderMarkdown(Catalogue));
    }

    private static IEnumerable<PersonalDataMemberBinding> Bindings() =>
        Catalogue.Fields.SelectMany(field => field.Bindings);

    private static void AssertBinding(Type type, string member, PersonalDataSurface surface) =>
        Assert.Contains(
            Bindings(),
            binding => binding.Assembly == type.Assembly.GetName().Name &&
                       binding.Type == type.FullName &&
                       binding.Member == member &&
                       binding.Surface == surface);

    private static PersonalDataCatalogDocument LoadCatalogue() => PersonalDataCatalogJson.Parse(
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "DataGovernance",
            "personal-data-catalog.v1.json")));
}
