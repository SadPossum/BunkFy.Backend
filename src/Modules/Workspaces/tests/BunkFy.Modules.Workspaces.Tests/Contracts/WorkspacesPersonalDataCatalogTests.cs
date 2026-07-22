namespace BunkFy.Modules.Workspaces.Tests;

using System.Reflection;
using BunkFy.DataGovernance;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.AdminApi;
using BunkFy.Modules.Workspaces.Api;
using BunkFy.Modules.Workspaces.Api.Requests;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Messaging;
using Gma.Framework.Scoping;
using Gma.Modules.Organizations.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesPersonalDataCatalogTests
{
    private static readonly PersonalDataCatalogDocument Catalogue = LoadCatalogue();
    private static readonly Dictionary<string, Assembly> Assemblies = CreateAssemblyIndex();
    private static readonly Dictionary<Type, HashSet<string>> NonPersonalMembers = new()
    {
        [typeof(WorkspaceStaffOnboardingListResponse)] = PaginationMembers(),
        [typeof(WorkspaceStaffAccessProcessListResponse)] = PaginationMembers(),
        [typeof(WorkspaceStaffJoinSourceListResponse)] = PaginationMembers()
    };

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
    public void Every_workspaces_owned_personal_persistence_member_is_classified()
    {
        using WorkspacesDbContext dbContext = CreateDbContext();
        foreach (Type entityType in PersistenceTypes())
        {
            IEntityType model = Assert.Single(
                dbContext.Model.GetEntityTypes(),
                candidate => candidate.ClrType == entityType);
            foreach (IProperty property in model.GetProperties().Where(property => !property.IsShadowProperty()))
            {
                AssertBinding(entityType, property.Name, PersonalDataSurface.Persistence);
            }
        }
    }

    [Fact]
    public void Every_selected_command_query_api_admin_response_and_consumed_event_member_is_classified()
    {
        foreach ((PersonalDataSurface surface, Type type) in BoundaryTypes())
        {
            AssertType(type, surface);
        }
    }

    [Fact]
    public void Direct_or_unstructured_applicant_data_cannot_enter_operational_outputs()
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

    private static Type[] PersistenceTypes() =>
    [
        typeof(WorkspaceStaffOnboarding),
        typeof(WorkspaceStaffAccessProcess),
        typeof(WorkspaceStaffAccessProfileSnapshot),
        typeof(WorkspaceStaffAccessPlan),
        typeof(WorkspaceStaffAccessPlanProperty)
    ];

    private static IEnumerable<(PersonalDataSurface Surface, Type Type)> BoundaryTypes()
    {
        foreach (Type type in new[]
                 {
                     typeof(SubmitWorkspaceStaffOnboardingRequest),
                     typeof(IssueWorkspaceInvitationRequest),
                     typeof(IssueWorkspaceEnrollmentLinkRequest),
                     typeof(ManageWorkspaceJoinSourceRequest),
                     typeof(ReplaceWorkspaceJoinSourceRequest),
                     typeof(UpdateWorkspaceMemberAccessRequest)
                 })
        {
            yield return (PersonalDataSurface.ApiInput, type);
        }

        foreach (Type type in new[]
                 {
                     typeof(SubmitWorkspaceStaffOnboardingCommand),
                     typeof(PrepareWorkspaceStaffAccessCommand),
                     typeof(DenyWorkspaceStaffAccessCommand),
                     typeof(RetryWorkspaceStaffAccessProcessCommand),
                     typeof(RetryWorkspaceStaffOnboardingCommand),
                     typeof(PrepareWorkspaceStaffAccessPlanCommand),
                     typeof(ActivateWorkspaceStaffAccessPlanCommand),
                     typeof(WorkspaceInvitationIssuanceRequest),
                     typeof(WorkspaceEnrollmentLinkIssuanceRequest),
                     typeof(WorkspaceMemberAccessUpdate)
                 })
        {
            yield return (PersonalDataSurface.ApplicationCommand, type);
        }

        yield return (PersonalDataSurface.ApplicationQuery, typeof(GetOwnWorkspaceStaffOnboardingQuery));
        yield return (PersonalDataSurface.ProjectionExport, typeof(WorkspaceStaffAccessPreparation));

        foreach (Type type in new[]
                 {
                     typeof(WorkspaceStaffOnboardingDto),
                     typeof(WorkspaceStaffOnboardingListResponse),
                     typeof(WorkspaceMemberAccessDto),
                     typeof(WorkspaceMemberAccessAssignmentDto),
                     typeof(WorkspaceStaffJoinSourceDto),
                     typeof(WorkspaceStaffJoinSourceListResponse),
                     typeof(WorkspaceStaffJoinSourceReplacementDto),
                     typeof(WorkspaceStaffAccessPlanDto),
                     typeof(WorkspaceStaffJoinSourceIssuanceDto)
                 })
        {
            yield return (PersonalDataSurface.ApiResponse, type);
        }

        yield return (PersonalDataSurface.AdminOutput, typeof(WorkspaceStaffAccessProcessDto));
        yield return (PersonalDataSurface.AdminOutput, typeof(WorkspaceStaffAccessProcessListResponse));

        foreach (Type type in new[]
                 {
                     typeof(OrganizationInvitationChangedIntegrationEvent),
                     typeof(OrganizationEnrollmentClaimChangedIntegrationEvent),
                     typeof(OrganizationEnrollmentLinkChangedIntegrationEvent),
                     typeof(OrganizationMembershipChangedIntegrationEvent),
                     typeof(StaffMemberLifecycleChangedIntegrationEvent)
                 })
        {
            yield return (PersonalDataSurface.IntegrationEvent, type);
        }
    }

    private static Dictionary<string, Assembly> CreateAssemblyIndex() =>
        new[]
        {
            typeof(WorkspacesAdminApiModule).Assembly,
            typeof(WorkspacesModule).Assembly,
            typeof(SubmitWorkspaceStaffOnboardingCommand).Assembly,
            typeof(WorkspacesModuleMetadata).Assembly,
            typeof(WorkspaceStaffOnboarding).Assembly,
            typeof(WorkspacesDbContext).Assembly,
            typeof(OrganizationInvitationChangedIntegrationEvent).Assembly,
            typeof(StaffMemberLifecycleChangedIntegrationEvent).Assembly
        }.ToDictionary(assembly => assembly.GetName().Name!, StringComparer.Ordinal);

    private static HashSet<string> PaginationMembers() =>
        new(["Page", "PageSize"], StringComparer.Ordinal);

    private static PersonalDataCatalogDocument LoadCatalogue() => PersonalDataCatalogJson.Parse(
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "DataGovernance",
            "personal-data-catalog.v1.json")));

    private static WorkspacesDbContext CreateDbContext()
    {
        DbContextOptions<WorkspacesDbContext> options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseInMemoryDatabase($"workspaces-data-catalog-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
