namespace BunkFy.Modules.Staff.Tests;

using System.Reflection;
using BunkFy.DataGovernance;
using BunkFy.Modules.Staff.AdminApi;
using BunkFy.Modules.Staff.Api;
using BunkFy.Modules.Staff.Api.Requests;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Persistence;
using Gma.Framework.Messaging;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffPersonalDataCatalogTests
{
    private static readonly PersonalDataCatalogDocument Catalogue = LoadCatalogue();
    private static readonly Dictionary<string, Assembly> Assemblies = CreateAssemblyIndex();
    private static readonly string[] ExpectedDirectoryMemberProperties =
        ["Assignments", "Department", "DisplayName", "JobTitle", "StaffMemberId", "Status", "Version"];
    private static readonly string[] ExpectedDirectoryAssignmentProperties =
        ["AssignmentId", "EffectiveFrom", "IsPrimary", "PropertyId", "PropertyJobTitle"];

    private static readonly Dictionary<Type, HashSet<string>> NonPersonalMembers = new()
    {
        [typeof(ListStaffMembersQuery)] = new(
            [nameof(ListStaffMembersQuery.Page), nameof(ListStaffMembersQuery.PageSize)],
            StringComparer.Ordinal),
        [typeof(ListStaffMembersAtPropertyQuery)] = new(
            [nameof(ListStaffMembersAtPropertyQuery.Page), nameof(ListStaffMembersAtPropertyQuery.PageSize)],
            StringComparer.Ordinal),
        [typeof(StaffDirectoryListResponse)] = new(
            [nameof(StaffDirectoryListResponse.Page), nameof(StaffDirectoryListResponse.PageSize)],
            StringComparer.Ordinal),
        [typeof(StaffIdentityReconciliationResult)] = new(
            [nameof(StaffIdentityReconciliationResult.IsSuccess), nameof(StaffIdentityReconciliationResult.ErrorCode)],
            StringComparer.Ordinal),
        [typeof(StaffOnboardingProvisioningResult)] = new(
            [nameof(StaffOnboardingProvisioningResult.IsSuccess), nameof(StaffOnboardingProvisioningResult.ErrorCode)],
            StringComparer.Ordinal),
        [typeof(StaffAdminApiModule.StaffAuthSubjectRequest)] = new(
            [nameof(StaffAdminApiModule.StaffAuthSubjectRequest.Confirmed)],
            StringComparer.Ordinal),
        [typeof(StaffAdminApiModule.StaffDepartureRequest)] = new(
            [nameof(StaffAdminApiModule.StaffDepartureRequest.Confirmed)],
            StringComparer.Ordinal)
    };

    [Fact]
    public void Every_catalogue_binding_resolves_to_a_real_member()
    {
        foreach (PersonalDataMemberBinding binding in Bindings())
        {
            Assert.True(Assemblies.TryGetValue(binding.Assembly, out Assembly? assembly),
                $"Unknown assembly '{binding.Assembly}'.");
            Type? type = assembly.GetType(binding.Type, throwOnError: false, ignoreCase: false);
            Assert.NotNull(type);
            Assert.NotNull(type.GetProperty(binding.Member, BindingFlags.Instance | BindingFlags.Public));
        }
    }

    [Fact]
    public void Every_staff_owned_personal_persistence_member_is_classified()
    {
        using StaffDbContext dbContext = CreateDbContext();
        foreach (Type entityType in new[] { typeof(StaffMember), typeof(StaffPropertyAssignment) })
        {
            IEntityType model = dbContext.Model.FindEntityType(entityType)!;
            foreach (IProperty property in model.GetProperties())
            {
                AssertBinding(entityType, property.Name, PersonalDataSurface.Persistence);
            }
        }

        foreach (string searchMember in new[]
                 {
                     nameof(StaffMember.DisplayNameSearch),
                     nameof(StaffMember.LegalNameSearch),
                     nameof(StaffMember.WorkEmailSearch),
                     nameof(StaffMember.WorkPhoneSearch),
                     nameof(StaffMember.EmployeeNumberSearch)
                 })
        {
            AssertBinding(typeof(StaffMember), searchMember, PersonalDataSurface.SearchIndex);
        }
    }

    [Fact]
    public void Every_selected_command_query_api_admin_contract_and_event_member_is_classified()
    {
        foreach ((PersonalDataSurface surface, Type type) in ContractTypes())
        {
            AssertType(type, surface);
        }
    }

    [Fact]
    public void Direct_or_unstructured_staff_data_cannot_enter_operational_outputs()
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
    public void Directory_contracts_exclude_sensitive_profile_and_history_fields()
    {
        string[] memberProperties = typeof(StaffDirectoryMemberDto).GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] assignmentProperties = typeof(StaffDirectoryAssignmentDto).GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ExpectedDirectoryMemberProperties.Order(StringComparer.Ordinal),
            memberProperties);
        Assert.Equal(
            ExpectedDirectoryAssignmentProperties.Order(StringComparer.Ordinal),
            assignmentProperties);
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
            bool isIntegrationEventMetadata = typeof(IIntegrationEvent).IsAssignableFrom(type) &&
                                              property.Name is "EventName" or "Version";
            if (isIntegrationEventMetadata ||
                (NonPersonalMembers.TryGetValue(type, out HashSet<string>? excluded) &&
                 excluded.Contains(property.Name)))
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
        Assembly application = typeof(CreateStaffMemberCommand).Assembly;
        foreach (Type type in application.GetTypes()
                     .Where(type => type.IsPublic && !type.IsAbstract)
                     .Where(type => type.Namespace?.StartsWith(
                                        "BunkFy.Modules.Staff.Application.Commands",
                                        StringComparison.Ordinal) == true ||
                                    type.Namespace?.StartsWith(
                                        "BunkFy.Modules.Staff.Application.Queries",
                                        StringComparison.Ordinal) == true)
                     .Where(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0))
        {
            PersonalDataSurface surface = type.Namespace!.Contains(".Queries", StringComparison.Ordinal)
                ? PersonalDataSurface.ApplicationQuery
                : PersonalDataSurface.ApplicationCommand;
            yield return (surface, type);
        }

        foreach (Type type in typeof(StaffModule).Assembly.GetTypes()
                     .Where(type => type.IsPublic && !type.IsAbstract)
                     .Where(type => type.Namespace == typeof(StaffProfileWriteRequest).Namespace)
                     .Where(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0))
        {
            yield return (PersonalDataSurface.ApiInput, type);
        }

        foreach (Type type in typeof(StaffAdminApiModule).GetNestedTypes(BindingFlags.Public)
                     .Where(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0))
        {
            yield return (PersonalDataSurface.AdminInput, type);
        }

        Assembly contracts = typeof(StaffModuleMetadata).Assembly;
        foreach (Type type in contracts.GetTypes()
                     .Where(type => type.IsPublic && !type.IsAbstract)
                     .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type) ||
                                    type.Name.EndsWith("Dto", StringComparison.Ordinal) ||
                                    type == typeof(StaffDirectoryListResponse) ||
                                    type == typeof(StaffIdentityReconciliationRequest) ||
                                    type == typeof(StaffIdentityReconciliationResult) ||
                                    type == typeof(StaffOnboardingProvisioningRequest) ||
                                    type == typeof(StaffOnboardingProvisioningResult))
                     .Where(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0))
        {
            if (typeof(IIntegrationEvent).IsAssignableFrom(type))
            {
                yield return (PersonalDataSurface.IntegrationEvent, type);
            }
            else if (type == typeof(StaffIdentityReconciliationRequest) ||
                     type == typeof(StaffOnboardingProvisioningRequest))
            {
                yield return (PersonalDataSurface.IntegrationCommand, type);
            }
            else if (type == typeof(StaffIdentityReconciliationResult) ||
                     type == typeof(StaffOnboardingProvisioningResult))
            {
                yield return (PersonalDataSurface.ProjectionExport, type);
            }
            else
            {
                yield return (PersonalDataSurface.ApiResponse, type);
                yield return (PersonalDataSurface.AdminOutput, type);
            }
        }

        Assembly domain = typeof(StaffMember).Assembly;
        foreach (Type type in domain.GetTypes()
                     .Where(type => type.IsPublic && !type.IsAbstract)
                     .Where(type => type.Namespace?.StartsWith(
                                        "BunkFy.Modules.Staff.Domain.Events",
                                        StringComparison.Ordinal) == true)
                     .Where(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0))
        {
            yield return (PersonalDataSurface.DomainEvent, type);
        }
    }

    private static Dictionary<string, Assembly> CreateAssemblyIndex() =>
        new[]
        {
            typeof(StaffAdminApiModule).Assembly,
            typeof(StaffModule).Assembly,
            typeof(CreateStaffMemberCommand).Assembly,
            typeof(StaffModuleMetadata).Assembly,
            typeof(StaffMember).Assembly,
            typeof(StaffDbContext).Assembly
        }.ToDictionary(assembly => assembly.GetName().Name!, StringComparer.Ordinal);

    private static PersonalDataCatalogDocument LoadCatalogue() => PersonalDataCatalogJson.Parse(
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "DataGovernance",
            "personal-data-catalog.v1.json")));

    private static StaffDbContext CreateDbContext()
    {
        DbContextOptions<StaffDbContext> options = new DbContextOptionsBuilder<StaffDbContext>()
            .UseInMemoryDatabase($"staff-data-catalog-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
