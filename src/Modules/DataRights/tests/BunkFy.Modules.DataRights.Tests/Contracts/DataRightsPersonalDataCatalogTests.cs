namespace BunkFy.Modules.DataRights.Tests.Contracts;

using System.Reflection;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Api;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Xunit;
using DomainSubjectCoordinate = DataRights.Domain.Entities.DataRightsSubjectCoordinate;
using RestoreCheckpoint = DataRights.Domain.Entities.DataRightsRestoreCheckpoint;
using ProcessingLedgerEntry =
    DataRights.Domain.Entities.DataRightsProcessingLedgerEntry;
using ProcessingLedgerSnapshot =
    DataRights.Domain.Models.DataRightsProcessingLedgerSnapshot;
using ExportAuditEntry =
    DataRights.Domain.Entities.DataRightsExportAuditEntry;

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
            typeof(ExecuteDataRightsRestrictionCommand),
            typeof(RecordControllerRoutingCommand),
            typeof(RecordRequesterVerificationCommand),
            typeof(PrepareDataRightsExportDownloadCommand),
            typeof(RequestDataRightsExportCommand),
            typeof(RequireDataRightsReviewCommand),
            typeof(SelectDataRightsSubjectCommand),
            typeof(StartDataRightsAnonymisationExecutionCommand),
            typeof(UnselectDataRightsSubjectCommand)
        ];

        foreach (Type command in commands)
        {
            AssertBinding(command, "ActorId", PersonalDataSurface.ApplicationCommand);
        }

        AssertBinding(
            typeof(DataRightsOperationApprovalRequest),
            nameof(DataRightsOperationApprovalRequest.ExecutingActorId),
            PersonalDataSurface.ApplicationQuery);
        AssertBinding(
            typeof(DataRightsAnonymisationContributionRequest),
            nameof(DataRightsAnonymisationContributionRequest.ExecutingActorId),
            PersonalDataSurface.IntegrationCommand);
        AssertBinding(
            typeof(DataRightsRestrictionContributionRequest),
            nameof(DataRightsRestrictionContributionRequest.ExecutingActorId),
            PersonalDataSurface.IntegrationCommand);
        AssertBinding(typeof(DataRightsCase), nameof(DataRightsCase.CreatedBy), PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsCase),
            nameof(DataRightsCase.ExecutionStartedBy),
            PersonalDataSurface.Persistence);
        AssertBinding(typeof(DataRightsCase), nameof(DataRightsCase.LastChangedBy), PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsExportArtifact),
            nameof(DataRightsExportArtifact.RequestedBy),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsExportArtifact),
            nameof(DataRightsExportArtifact.GenerationActor),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(ExportAuditEntry),
            nameof(ExportAuditEntry.ActorId),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsExecutionBatch),
            nameof(DataRightsExecutionBatch.CreatedBy),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsExecutionWorkItem),
            nameof(DataRightsExecutionWorkItem.CreatedBy),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DomainSubjectCoordinate),
            nameof(DomainSubjectCoordinate.SelectedBy),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsRestrictionExecutionProof),
            nameof(DataRightsRestrictionExecutionProof.ExecutedBy),
            PersonalDataSurface.Persistence);
    }

    [Fact]
    public void Discovery_personal_data_is_explicitly_classified()
    {
        Type apiRequest = typeof(DataRightsModule).Assembly.GetType(
            "BunkFy.Modules.DataRights.Api.DataRightsDiscoveryEndpoints+" +
            "DiscoverDataRightsSubjectsRequest",
            throwOnError: true)!;
        foreach (string member in new[]
                 {
                     "RecordId",
                     "Email",
                     "Phone",
                     "Name",
                     "DateOfBirth",
                     "AccountSubjectId"
                 })
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
            typeof(DataRightsExecutionWorkItem),
            nameof(DataRightsExecutionWorkItem.RecordId),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsSelectedSubjectDto),
            nameof(DataRightsSelectedSubjectDto.RecordId),
            PersonalDataSurface.ApiResponse);
        AssertBinding(
            typeof(DataRightsExecutionWorkItemDto),
            nameof(DataRightsExecutionWorkItemDto.RecordId),
            PersonalDataSurface.ApiResponse);
        AssertBinding(
            typeof(DataRightsAnonymisationContributionRequest),
            nameof(DataRightsAnonymisationContributionRequest.Coordinate),
            PersonalDataSurface.IntegrationCommand);
        AssertBinding(
            typeof(DataRightsRestrictionContributionRequest),
            nameof(DataRightsRestrictionContributionRequest.Coordinate),
            PersonalDataSurface.IntegrationCommand);
        AssertBinding(
            typeof(DataRightsRestrictionExecutionProof),
            nameof(DataRightsRestrictionExecutionProof.RecordId),
            PersonalDataSurface.Persistence);
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
    public void Owner_export_envelope_is_explicitly_classified()
    {
        foreach (string member in new[] { "RecordType", "RecordId", "RecordVersion", "Fields" })
        {
            AssertBinding(typeof(DataRightsExportRecord), member, PersonalDataSurface.DataRightsExport);
        }

        AssertBinding(
            typeof(DataRightsExportField),
            nameof(DataRightsExportField.FieldId),
            PersonalDataSurface.DataRightsExport);
        AssertBinding(
            typeof(DataRightsExportField),
            nameof(DataRightsExportField.Value),
            PersonalDataSurface.DataRightsExport);
    }

    [Fact]
    public void Protected_export_digests_are_explicitly_classified()
    {
        AssertBinding(
            typeof(DataRightsExportArtifact),
            nameof(DataRightsExportArtifact.SelectionSha256),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsExportArtifact),
            nameof(DataRightsExportArtifact.PlaintextSha256),
            PersonalDataSurface.Persistence);
    }

    [Fact]
    public void Restriction_execution_proof_is_explicitly_classified()
    {
        foreach (PropertyInfo property in typeof(DataRightsRestrictionContributionRequest)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.Name is not
                         nameof(DataRightsRestrictionContributionRequest.Coordinate) and not
                         nameof(DataRightsRestrictionContributionRequest.ExecutingActorId)))
        {
            AssertBinding(
                typeof(DataRightsRestrictionContributionRequest),
                property.Name,
                PersonalDataSurface.IntegrationCommand);
        }

        foreach (PropertyInfo property in typeof(DataRightsRestrictionOwnerProof)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            AssertBinding(
                typeof(DataRightsRestrictionOwnerProof),
                property.Name,
                PersonalDataSurface.ProjectionExport);
        }

        AssertBinding(
            typeof(DataRightsRestrictionContributionResult),
            nameof(DataRightsRestrictionContributionResult.OwnerProof),
            PersonalDataSurface.ProjectionExport);
        AssertBinding(
            typeof(DataRightsRestrictionExecutionDto),
            nameof(DataRightsRestrictionExecutionDto.Proof),
            PersonalDataSurface.ApiResponse);
        foreach (PropertyInfo property in typeof(DataRightsRestrictionExecutionProofDto)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            AssertBinding(
                typeof(DataRightsRestrictionExecutionProofDto),
                property.Name,
                PersonalDataSurface.ApiResponse);
        }

        foreach (PropertyInfo property in typeof(DataRightsRestrictionExecutionProof)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.Name is not
                         nameof(DataRightsRestrictionExecutionProof.RecordId) and not
                         nameof(DataRightsRestrictionExecutionProof.ExecutedBy)))
        {
            AssertBinding(
                typeof(DataRightsRestrictionExecutionProof),
                property.Name,
                PersonalDataSurface.Persistence);
        }
    }

    [Fact]
    public void Case_contracts_do_not_carry_direct_guest_payloads()
    {
        Type[] boundaryTypes =
        [
            typeof(DataRightsModule.CreateDataRightsCaseRequest),
            typeof(DataRightsModule.RecordDataRightsDecisionRequest),
            typeof(DataRightsModule.RecordRequesterVerificationRequest),
            typeof(DataRightsModule.VersionedDataRightsCaseRequest),
            typeof(BeginDataRightsDecisionCommand),
            typeof(BeginDataRightsDiscoveryCommand),
            typeof(CancelDataRightsCaseCommand),
            typeof(CreateDataRightsCaseCommand),
            typeof(RecordControllerRoutingCommand),
            typeof(RecordDataRightsDecisionCommand),
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
            ["CreatedBy", "DecidedBy", "ExecutionStartedBy", "LastChangedBy", "ScopeId"],
            typeof(DataRightsCase)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.PropertyType == typeof(string))
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Processing_ledger_pseudonym_is_explicitly_classified()
    {
        AssertBinding(
            typeof(DataRightsRecordPseudonym),
            nameof(DataRightsRecordPseudonym.Sha256),
            PersonalDataSurface.ApplicationQuery);
        AssertBinding(
            typeof(ProcessingLedgerEntry),
            nameof(ProcessingLedgerEntry.RecordPseudonymSha256),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(ProcessingLedgerSnapshot),
            nameof(ProcessingLedgerSnapshot.RecordPseudonymSha256),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(DataRightsProtectedReplayEnvelope),
            nameof(DataRightsProtectedReplayEnvelope.CiphertextBase64),
            PersonalDataSurface.Persistence);
    }

    [Fact]
    public void Restore_contracts_and_checkpoint_are_explicitly_classified()
    {
        AssertBinding(
            typeof(DataRightsAnonymisationRestoreRequest),
            nameof(DataRightsAnonymisationRestoreRequest.RecordId),
            PersonalDataSurface.IntegrationCommand);

        foreach (PropertyInfo property in typeof(DataRightsAnonymisationRestoreRequest)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.Name != nameof(DataRightsAnonymisationRestoreRequest.RecordId)))
        {
            AssertBinding(
                typeof(DataRightsAnonymisationRestoreRequest),
                property.Name,
                PersonalDataSurface.IntegrationCommand);
        }

        foreach (PropertyInfo property in typeof(DataRightsAnonymisationRestoreProof)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            AssertBinding(
                typeof(DataRightsAnonymisationRestoreProof),
                property.Name,
                PersonalDataSurface.ProjectionExport);
        }

        AssertBinding(
            typeof(ProcessingLedgerEntry),
            nameof(ProcessingLedgerEntry.ResultingRecordVersion),
            PersonalDataSurface.Persistence);
        AssertBinding(
            typeof(ProcessingLedgerSnapshot),
            nameof(ProcessingLedgerSnapshot.ResultingRecordVersion),
            PersonalDataSurface.Persistence);

        AssertBinding(
            typeof(DataRightsAnonymisationRestoreResult),
            nameof(DataRightsAnonymisationRestoreResult.Proof),
            PersonalDataSurface.ProjectionExport);

        foreach (PropertyInfo property in typeof(RestoreCheckpoint)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.Name != nameof(RestoreCheckpoint.Cursor)))
        {
            AssertBinding(
                typeof(RestoreCheckpoint),
                property.Name,
                PersonalDataSurface.Persistence);
        }
    }

    [Fact]
    public void Anonymisation_event_and_task_payload_are_pii_free_coordinates()
    {
        Assert.Equal(
            [
                "BatchId",
                "CaseId",
                "EventId",
                "EventName",
                "ExecutionRevision",
                "OccurredAtUtc",
                "PropertyId",
                "ScopeId",
                "TenantId",
                "Version",
                "WorkItemId"
            ],
            typeof(DataRightsAnonymisationWorkItemTerminalIntegrationEvent)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "ApprovalRevision",
                "CaseId",
                "EventId",
                "EventName",
                "ExecutionRevision",
                "OccurredAtUtc",
                "PropertyId",
                "ScopeId",
                "TenantId",
                "Version",
                "WorkItemId"
            ],
            typeof(DataRightsAnonymisationExecutionPreparedIntegrationEvent)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "ApprovalRevision",
                "CaseId",
                "ExecutionRevision",
                "PropertyId",
                "WorkItemId"
            ],
            typeof(ExecuteDataRightsAnonymisationPayload)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "ArtifactId",
                "CaseId",
                "CaseType",
                "DecisionRevision",
                "EventId",
                "EventName",
                "ExpiresAtUtc",
                "OccurredAtUtc",
                "PropertyId",
                "ScopeId",
                "TenantId",
                "Version"
            ],
            typeof(DataRightsExportArtifactRequestedIntegrationEvent)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "ArtifactId",
                "CaseId",
                "CaseType",
                "DecisionRevision",
                "PropertyId"
            ],
            typeof(GenerateDataRightsExportPayload)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "ArtifactId",
                "CaseId",
                "CaseType",
                "DecisionRevision",
                "ExpiresAtUtc",
                "PropertyId"
            ],
            typeof(DeleteExpiredDataRightsExportArtifactPayload)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
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
