namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using BunkFy.Modules.Workspaces.Domain.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffOnboardingCorrectionReceiptTests
{
    private static readonly Guid ReceiptId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ExecutionId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid ApplicationId =
        Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid ApplicantEventId =
        Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid CompletionEventId =
        Guid.Parse("60000000-0000-0000-0000-000000000001");
    private const string TenantId =
        "70000000-0000-0000-0000-000000000001";
    private const string Digest =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTimeOffset CompletedAt =
        new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receipt_binds_exact_replay_and_raises_pii_free_event()
    {
        WorkspaceStaffOnboardingCorrectionReceipt receipt =
            Create().Value;

        Assert.True(receipt.MatchesReplay(
            CaseId,
            approvalRevision: 4,
            ApplicationId,
            selectedRecordVersion: 7,
            Digest.ToUpperInvariant()));
        Assert.False(receipt.MatchesReplay(
            CaseId,
            approvalRevision: 4,
            ApplicationId,
            selectedRecordVersion: 7,
            new string('b', 64)));
        Assert.Equal(
            [
                WorkspaceStaffOnboardingApplicantField.DisplayName,
                WorkspaceStaffOnboardingApplicantField.WorkEmail
            ],
            receipt.ChangedFields);

        WorkspaceStaffOnboardingCorrectionAppliedDomainEvent domainEvent =
            Assert.IsType<WorkspaceStaffOnboardingCorrectionAppliedDomainEvent>(
                Assert.Single(receipt.DomainEvents));
        Assert.Equal(CompletionEventId, domainEvent.EventId);
        Assert.Equal(ExecutionId, domainEvent.ExecutionId);
        Assert.Equal(ApplicationId, domainEvent.ApplicationId);
        Assert.Equal(7, domainEvent.SelectedRecordVersion);
        Assert.Equal(8, domainEvent.CurrentRecordVersion);
        Assert.DoesNotContain(
            domainEvent.GetType().GetProperties(),
            property => property.Name.Contains(
                "Name",
                StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains(
                    "Email",
                    StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains(
                    "Phone",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Receipt_rejects_invalid_identity_version_and_fields()
    {
        Assert.Equal(
            WorkspaceStaffOnboardingErrors.CorrectionReceiptIdentityInvalid,
            Create(executionId: Guid.Empty).Error);
        Assert.Equal(
            WorkspaceStaffOnboardingErrors.CorrectionReceiptVersionInvalid,
            Create(currentRecordVersion: 7).Error);
        Assert.Equal(
            WorkspaceStaffOnboardingErrors.CorrectionReceiptFieldsInvalid,
            Create(changedFields: []).Error);
        Assert.Equal(
            WorkspaceStaffOnboardingErrors.CorrectionReceiptFieldsInvalid,
            Create(
                changedFields:
                [WorkspaceStaffOnboardingApplicantField.Unknown]).Error);
    }

    [Fact]
    public async Task Receipt_event_projects_one_pii_free_completion()
    {
        WorkspaceStaffOnboardingCorrectionReceipt receipt =
            Create().Value;
        WorkspaceStaffOnboardingCorrectionAppliedDomainEvent domainEvent =
            Assert.IsType<WorkspaceStaffOnboardingCorrectionAppliedDomainEvent>(
                Assert.Single(receipt.DomainEvents));
        RecordingOutbox outbox = new();

        await new
                WorkspaceStaffOnboardingDataRightsCorrectionAppliedOutboxProjector(
                    new RecordingOutboxRegistry(outbox))
            .HandleAsync(domainEvent, CancellationToken.None);

        DataRightsTenantCorrectionAppliedIntegrationEvent completion =
            Assert.Single(outbox.Events.OfType<
                DataRightsTenantCorrectionAppliedIntegrationEvent>());
        Assert.Equal(CompletionEventId, completion.EventId);
        Assert.Equal(ExecutionId, completion.ExecutionId);
        Assert.Equal(DataRightsCaseType.StaffRights, completion.CaseType);
        Assert.Equal(
            WorkspacesDataRightsCoordinates.Owner,
            completion.OwnerKey);
        Assert.Equal(
            WorkspacesDataRightsCoordinates.StaffOnboardingRecordType,
            completion.RecordType);
        Assert.Equal(
            [
                WorkspaceStaffOnboardingDataRightsFieldKeys.DisplayName,
                WorkspaceStaffOnboardingDataRightsFieldKeys.WorkEmail
            ],
            completion.ChangedFieldKeys);
        Assert.DoesNotContain(
            outbox.Events.SelectMany(item =>
                item.GetType().GetProperties()),
            property => new[]
            {
                "DisplayName",
                "LegalName",
                "WorkEmail",
                "WorkPhone",
                "EmployeeNumber",
                "JobTitle",
                "Department",
                "ActorId",
                "RequestSha256"
            }.Contains(property.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static Result<WorkspaceStaffOnboardingCorrectionReceipt> Create(
        Guid? executionId = null,
        long currentRecordVersion = 8,
        IReadOnlyCollection<WorkspaceStaffOnboardingApplicantField>?
            changedFields = null) =>
        WorkspaceStaffOnboardingCorrectionReceipt.Create(
            ReceiptId,
            TenantId,
            executionId ?? ExecutionId,
            CaseId,
            approvalRevision: 4,
            ApplicationId,
            selectedRecordVersion: 7,
            currentRecordVersion,
            changedFields ??
            [
                WorkspaceStaffOnboardingApplicantField.WorkEmail,
                WorkspaceStaffOnboardingApplicantField.DisplayName,
                WorkspaceStaffOnboardingApplicantField.WorkEmail
            ],
            Digest,
            ApplicantEventId,
            CompletionEventId,
            CompletedAt);

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => WorkspacesModuleMetadata.Name;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRegistry(RecordingOutbox outbox)
        : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName)
        {
            Assert.Equal(WorkspacesModuleMetadata.Name, moduleName);
            return outbox;
        }
    }
}
