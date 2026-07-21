namespace BunkFy.Modules.Ingestion.Tests;

using System.Reflection;
using BunkFy.Adapter.Abstractions;
using BunkFy.DataGovernance;
using BunkFy.Modules.Ingestion.Api;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Runs;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.ObservationParsing;
using Gma.Framework.Messaging;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionPersonalDataCatalogTests
{
    private static readonly PersonalDataCatalogDocument Catalogue = LoadCatalogue();
    private static readonly Dictionary<string, Assembly> Assemblies = CreateAssemblyIndex();

    [Fact]
    public void Every_catalogue_binding_resolves_to_a_real_member()
    {
        foreach (PersonalDataMemberBinding binding in Bindings())
        {
            Assert.True(
                Assemblies.TryGetValue(binding.Assembly, out Assembly? assembly),
                $"Unknown assembly '{binding.Assembly}'.");
            Type? type = assembly.GetType(binding.Type, throwOnError: false, ignoreCase: false);
            Assert.NotNull(type);
            Assert.NotNull(type.GetProperty(binding.Member, BindingFlags.Instance | BindingFlags.Public));
        }
    }

    [Fact]
    public void Every_ingestion_owned_persistence_member_is_classified()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        foreach (IEntityType entityType in dbContext.Model.GetEntityTypes()
                     .Where(IsProductPersistenceType))
        {
            foreach (IProperty property in entityType.GetProperties())
            {
                AssertBinding(entityType.ClrType, property.Name, PersonalDataSurface.Persistence);
            }
        }
    }

    [Fact]
    public void Every_selected_command_query_api_contract_and_adapter_member_is_classified()
    {
        foreach ((PersonalDataSurface surface, Type type) in ContractTypes())
        {
            AssertType(type, surface);
        }
    }

    [Fact]
    public void Direct_or_unstructured_data_cannot_enter_operational_outputs()
    {
        HashSet<PersonalDataClassification> restricted =
        [
            PersonalDataClassification.DirectIdentifier,
            PersonalDataClassification.Contact,
            PersonalDataClassification.Demographic,
            PersonalDataClassification.Preference,
            PersonalDataClassification.FreeText,
            PersonalDataClassification.SearchInput,
            PersonalDataClassification.StructuredPayload
        ];
        HashSet<PersonalDataSurface> prohibited =
        [
            PersonalDataSurface.IntegrationEvent,
            PersonalDataSurface.Notification,
            PersonalDataSurface.Log,
            PersonalDataSurface.Metric,
            PersonalDataSurface.Trace,
            PersonalDataSurface.SupportBundle
        ];

        PersonalDataFieldDefinition[] offenders = Catalogue.Fields
            .Where(field => restricted.Contains(field.Classification))
            .Where(field => field.AllowedSurfaces.Any(prohibited.Contains) ||
                            field.Bindings.Any(binding => prohibited.Contains(binding.Surface)))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Raw_source_evidence_stays_inside_explicit_ingress_command_file_and_response_surfaces()
    {
        HashSet<PersonalDataSurface> allowed =
        [
            PersonalDataSurface.AdapterIngress,
            PersonalDataSurface.ApplicationCommand,
            PersonalDataSurface.FileIngress,
            PersonalDataSurface.ApiResponse
        ];
        PersonalDataFieldDefinition[] rawFields = Catalogue.Fields
            .Where(field => field.Id.StartsWith("ingestion.raw-source.", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(rawFields);
        Assert.All(rawFields, field =>
        {
            Assert.Equal(PersonalDataClassification.StructuredPayload, field.Classification);
            Assert.Equal(PersonalDataSensitivity.Unstructured, field.Sensitivity);
            Assert.All(field.AllowedSurfaces, surface => Assert.Contains(surface, allowed));
            Assert.All(field.Bindings, binding => Assert.Contains(binding.Surface, allowed));
        });
    }

    [Fact]
    public void Cross_module_event_surface_is_minimal_and_contains_no_sensitive_fields()
    {
        Type[] eventTypes = typeof(IngestionModuleMetadata).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IIntegrationEvent).IsAssignableFrom(type))
            .ToArray();
        Assert.Equal([typeof(ObservationReceiptAcceptedIntegrationEvent)], eventTypes);

        PersonalDataFieldDefinition[] eventFields = Catalogue.Fields
            .Where(field => field.Bindings.Any(binding => binding.Surface == PersonalDataSurface.IntegrationEvent))
            .ToArray();
        Assert.NotEmpty(eventFields);
        Assert.DoesNotContain(eventFields, field =>
            field.Classification is PersonalDataClassification.DirectIdentifier or
                PersonalDataClassification.Contact or
                PersonalDataClassification.Demographic or
                PersonalDataClassification.Preference or
                PersonalDataClassification.FreeText or
                PersonalDataClassification.SearchInput or
                PersonalDataClassification.StructuredPayload);
    }

    [Fact]
    public void Persisted_and_remote_operational_failures_expose_only_stable_error_codes()
    {
        Type[] durableOrRemoteTypes =
        [
            typeof(IngestionRun),
            typeof(IngestionRunDto),
            typeof(AdapterConnectionHealthDto),
            typeof(AdapterRemoteRunCompletionRequest)
        ];

        Assert.All(durableOrRemoteTypes, type =>
        {
            Assert.Null(type.GetProperty("ErrorMessage", BindingFlags.Instance | BindingFlags.Public));
            Assert.True(
                type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Any(property => property.Name is "ErrorCode" or "LatestRunErrorCode"),
                $"{type.FullName} must expose a stable error-code member.");
        });
        Assert.InRange(AdapterProtocolLimits.ErrorCodeMaxLength, 1, 200);
    }

    [Fact]
    public void Checked_in_inventory_matches_deterministic_catalogue_rendering()
    {
        string expected = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "DataGovernance",
                "personal-data-inventory.v1.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(expected, PersonalDataInventoryRenderer.RenderMarkdown(Catalogue));
    }

    private static void AssertType(Type type, PersonalDataSurface surface)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (typeof(IIntegrationEvent).IsAssignableFrom(type) && property.Name is "EventName" or "Version")
            {
                continue;
            }

            AssertBinding(type, property.Name, surface);
        }
    }

    private static void AssertBinding(Type type, string member, PersonalDataSurface surface)
    {
        bool found = Bindings().Any(binding =>
            string.Equals(binding.Assembly, type.Assembly.GetName().Name, StringComparison.Ordinal) &&
            string.Equals(binding.Type, type.FullName, StringComparison.Ordinal) &&
            string.Equals(binding.Member, member, StringComparison.Ordinal) &&
            binding.Surface == surface);
        Assert.True(found, $"Missing {surface} classification for {type.FullName}.{member}.");
    }

    private static IEnumerable<PersonalDataMemberBinding> Bindings() =>
        Catalogue.Fields.SelectMany(field => field.Bindings);

    private static IEnumerable<(PersonalDataSurface Surface, Type Type)> ContractTypes()
    {
        Assembly application = typeof(CompleteAdapterRunCommand).Assembly;
        foreach (Type type in application.GetTypes()
                     .Where(IsPublicDataType)
                     .Where(type => type.Namespace?.StartsWith(
                                        "BunkFy.Modules.Ingestion.Application.Commands",
                                        StringComparison.Ordinal) == true ||
                                    type.Namespace?.StartsWith(
                                        "BunkFy.Modules.Ingestion.Application.Queries",
                                        StringComparison.Ordinal) == true ||
                                    type.Namespace?.StartsWith(
                                        "BunkFy.Modules.Ingestion.Application.Ports",
                                        StringComparison.Ordinal) == true ||
                                    type.Namespace?.StartsWith(
                                        "BunkFy.Modules.Ingestion.Application.Reservations",
                                        StringComparison.Ordinal) == true))
        {
            PersonalDataSurface surface = type.Name == "ObservationRawPayload"
                ? PersonalDataSurface.ApiResponse
                : type.Name is "RawPayloadRead" or "RawPayloadWrite"
                    ? PersonalDataSurface.FileIngress
                    : type.Namespace!.Contains(".Queries", StringComparison.Ordinal)
                        ? PersonalDataSurface.ApplicationQuery
                        : PersonalDataSurface.ApplicationCommand;
            yield return (surface, type);
        }

        foreach (Type type in typeof(IngestionModule).GetNestedTypes(BindingFlags.Public).Where(IsPublicDataType))
        {
            yield return (PersonalDataSurface.ApiInput, type);
        }

        Assembly contracts = typeof(IngestionModuleMetadata).Assembly;
        foreach (Type type in contracts.GetTypes().Where(IsPublicDataType))
        {
            PersonalDataSurface? surface = type.Namespace switch
            {
                _ when typeof(IIntegrationEvent).IsAssignableFrom(type) => PersonalDataSurface.IntegrationEvent,
                string value when value.Contains(".Api", StringComparison.Ordinal) => PersonalDataSurface.ApiResponse,
                string value when value.Contains(".Tasks", StringComparison.Ordinal) => PersonalDataSurface.ApplicationCommand,
                string value when value.Contains(".Adapters", StringComparison.Ordinal) => PersonalDataSurface.ApplicationQuery,
                _ => null
            };
            if (surface.HasValue)
            {
                yield return (surface.Value, type);
            }
        }

        foreach (Type type in typeof(AdapterIngressObservationRequest).Assembly.GetTypes().Where(IsPublicDataType))
        {
            yield return (PersonalDataSurface.AdapterIngress, type);
        }

        foreach (Type type in typeof(ObservationParserInput).Assembly.GetTypes().Where(IsPublicDataType))
        {
            yield return (PersonalDataSurface.FileIngress, type);
        }
    }

    private static bool IsProductPersistenceType(IEntityType entityType) =>
        entityType.ClrType.Namespace?.StartsWith("BunkFy.Modules.Ingestion", StringComparison.Ordinal) == true &&
        entityType.ClrType.Name != "IngestionProjectionRebuildCheckpoint";

    private static bool IsPublicDataType(Type type) =>
        type.IsPublic && !type.IsAbstract && type.IsClass &&
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0;

    private static Dictionary<string, Assembly> CreateAssemblyIndex() =>
        new[]
        {
            typeof(IngestionModule).Assembly,
            typeof(CompleteAdapterRunCommand).Assembly,
            typeof(IngestionModuleMetadata).Assembly,
            typeof(IngestionRun).Assembly,
            typeof(IngestionDbContext).Assembly,
            typeof(AdapterIngressObservationRequest).Assembly,
            typeof(ObservationParserInput).Assembly
        }.ToDictionary(assembly => assembly.GetName().Name!, StringComparer.Ordinal);

    private static PersonalDataCatalogDocument LoadCatalogue() => PersonalDataCatalogJson.Parse(
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "DataGovernance",
            "personal-data-catalog.v1.json")));

    private static IngestionDbContext CreateDbContext()
    {
        DbContextOptions<IngestionDbContext> options = new DbContextOptionsBuilder<IngestionDbContext>()
            .UseInMemoryDatabase($"ingestion-data-catalog-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
