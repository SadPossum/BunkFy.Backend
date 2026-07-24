namespace BunkFy.Modules.DataRights.Tests.Contracts;

using System.Reflection;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Api;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Persistence;
using Xunit;
using DomainSubjectCoordinate = DataRights.Domain.Entities.DataRightsSubjectCoordinate;

[Trait("Category", "Unit")]
public sealed class DataRightsPersonalDataCatalogTests
{
    private static readonly PersonalDataCatalogDocument Catalogue = LoadCatalogue();
    private static readonly Dictionary<string, Assembly> Assemblies = CreateAssemblyIndex();

    [Fact]
    public void Every_catalogue_binding_resolves_to_a_real_public_member()
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
    public void Every_case_actor_coordinate_is_classified()
    {
        Type[] commands =
        [
            typeof(BeginDataRightsDiscoveryCommand),
            typeof(CancelDataRightsCaseCommand),
            typeof(CreateDataRightsCaseCommand),
            typeof(RecordControllerRoutingCommand),
            typeof(RecordRequesterVerificationCommand),
            typeof(RequireDataRightsReviewCommand),
            typeof(SelectDataRightsSubjectCommand),
            typeof(UnselectDataRightsSubjectCommand)
        ];

        foreach (Type command in commands)
        {
            AssertBinding(command, "ActorId", PersonalDataSurface.ApplicationCommand);
        }

        AssertBinding(typeof(DataRightsCase), nameof(DataRightsCase.CreatedBy), PersonalDataSurface.Persistence);
        AssertBinding(typeof(DataRightsCase), nameof(DataRightsCase.LastChangedBy), PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DomainSubjectCoordinate),
            nameof(DomainSubjectCoordinate.SelectedBy),
            PersonalDataSurface.Persistence);
    }

    [Fact]
    public void Discovery_personal_data_is_explicitly_classified()
    {
        Type apiRequest = typeof(DataRightsModule).Assembly.GetType(
            "BunkFy.Modules.DataRights.Api.DataRightsDiscoveryEndpoints+" +
            "DiscoverDataRightsSubjectsRequest",
            throwOnError: true)!;
        foreach (string member in new[] { "RecordId", "Email", "Phone", "Name", "DateOfBirth" })
        {
            AssertBinding(apiRequest, member, PersonalDataSurface.ApiInput);
            AssertBinding(typeof(DataRightsSubjectLookup), member, PersonalDataSurface.ApplicationQuery);
        }

        AssertBinding(
            typeof(DataRightsSubjectCoordinate),
            nameof(DataRightsSubjectCoordinate.RecordId),
            PersonalDataSurface.ApiInput);
        AssertBinding(
            typeof(DataRightsSubjectCoordinate),
            nameof(DataRightsSubjectCoordinate.RecordId),
            PersonalDataSurface.ApiResponse);
        AssertBinding(
            typeof(DataRightsSubjectCoordinateKey),
            nameof(DataRightsSubjectCoordinateKey.RecordId),
            PersonalDataSurface.ApiInput);
        AssertBinding(
            typeof(DomainSubjectCoordinate),
            nameof(DomainSubjectCoordinate.RecordId),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsSelectedSubjectDto),
            nameof(DataRightsSelectedSubjectDto.RecordId),
            PersonalDataSurface.ApiResponse);
        AssertBinding(
            typeof(DataRightsSelectedSubjectDto),
            nameof(DataRightsSelectedSubjectDto.SelectedAtUtc),
            PersonalDataSurface.ApiResponse);
        AssertBinding(
            typeof(DomainSubjectCoordinate),
            nameof(DomainSubjectCoordinate.SelectedAtUtc),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsSubjectCandidate),
            nameof(DataRightsSubjectCandidate.DisplayName),
            PersonalDataSurface.ApiResponse);
        AssertBinding(
            typeof(DataRightsSubjectCandidate),
            nameof(DataRightsSubjectCandidate.EmailHint),
            PersonalDataSurface.ApiResponse);
        AssertBinding(
            typeof(DataRightsSubjectCandidate),
            nameof(DataRightsSubjectCandidate.PhoneHint),
            PersonalDataSurface.ApiResponse);
    }

    [Fact]
    public void Case_contracts_do_not_carry_direct_guest_payloads()
    {
        Type[] boundaryTypes =
        [
            typeof(DataRightsModule.CreateDataRightsCaseRequest),
            typeof(DataRightsModule.RecordRequesterVerificationRequest),
            typeof(DataRightsModule.VersionedDataRightsCaseRequest),
            typeof(BeginDataRightsDiscoveryCommand),
            typeof(CancelDataRightsCaseCommand),
            typeof(CreateDataRightsCaseCommand),
            typeof(RecordControllerRoutingCommand),
            typeof(RecordRequesterVerificationCommand),
            typeof(RequireDataRightsReviewCommand),
            typeof(SelectDataRightsSubjectCommand),
            typeof(UnselectDataRightsSubjectCommand),
            typeof(DataRightsSelectedSubjectDto),
            typeof(DataRightsCaseDto)
        ];
        string[] prohibitedNameParts =
        [
            "Address",
            "Birth",
            "Contact",
            "Document",
            "Email",
            "FreeText",
            "GuestName",
            "LegalName",
            "Nationality",
            "Note",
            "Passport",
            "Phone",
            "SearchText"
        ];

        string[] offenders = boundaryTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(property => prohibitedNameParts.Any(part =>
                property.Name.Contains(part, StringComparison.OrdinalIgnoreCase)))
            .Select(property => $"{property.DeclaringType!.FullName}.{property.Name}")
            .ToArray();

        Assert.Empty(offenders);
        Assert.Equal(
            ["CreatedBy", "LastChangedBy", "ScopeId"],
            typeof(DataRightsCase)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.PropertyType == typeof(string))
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
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

    private static Dictionary<string, Assembly> CreateAssemblyIndex() =>
        new[]
        {
            typeof(DataRightsModule).Assembly,
            typeof(CreateDataRightsCaseCommand).Assembly,
            typeof(DataRightsCaseDto).Assembly,
            typeof(DataRightsCase).Assembly,
            typeof(DataRightsDbContext).Assembly
        }.ToDictionary(assembly => assembly.GetName().Name!, StringComparer.Ordinal);

    private static PersonalDataCatalogDocument LoadCatalogue() => PersonalDataCatalogJson.Parse(
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "DataGovernance",
            "personal-data-catalog.v1.json")));
}
