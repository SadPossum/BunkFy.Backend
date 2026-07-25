namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.DataRights.Tests.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsRestoreCheckpointHandlerTests
{
    [Fact]
    public async Task Prepared_ledger_requires_exact_owner_proof_and_final_hash()
    {
        HmacDataRightsRecordPseudonymizer pseudonymizer =
            ProtectedLedgerTestData.CreatePseudonymizer((1, 'a'));
        AesGcmDataRightsReplayEnvelopeProtector protector =
            ProtectedLedgerTestData.CreateProtector(
                pseudonymizer,
                activeKeyVersion: 1,
                (1, 'r'));
        Guid recordId = Guid.NewGuid();
        DataRightsProcessingLedgerEntry ledger =
            ProtectedLedgerTestData.CreateLedger(
                pseudonymizer,
                "tenant-a",
                sequence: 1,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
                recordId);
        DataRightsLedgerDelta delta =
            ProtectedLedgerTestData.CreateDelta(
                protector,
                ledger,
                recordId);
        RecordingCheckpointRepository checkpoints = new();
        RecordingLedgerRepository ledgers = new();
        TestScopeContext scope = new();
        PrepareDataRightsRestoreBatchCommandHandler prepare =
            new(checkpoints, ledgers, scope);

        Result<Unit> prepared = await prepare.HandleAsync(
            new PrepareDataRightsRestoreBatchCommand(
                ExpectedCheckpointVersion: 0,
                DataRightsRestoreCursor.Genesis,
                [delta]),
            CancellationToken.None);

        Assert.True(prepared.IsSuccess);
        Assert.Equal(
            ledger.Freeze(),
            ledgers.Entries.Single().Freeze());
        DataRightsRestoreOwnerProofBinding proof = new(
            ledger.Id,
            ledger.TenantSequence,
            ledger.EntrySha256,
            ledger.OwnerReceiptId,
            ledger.OwnerReceiptSha256,
            ResultingRecordVersion: 4,
            TombstoneRevision: 1,
            ProtectedLedgerTestData.Now.AddHours(1));
        AdvanceDataRightsRestoreCheckpointCommandHandler advance =
            new(checkpoints, ledgers, scope);

        Result<DataRightsRestoreCheckpointState> invalid =
            await advance.HandleAsync(
                new AdvanceDataRightsRestoreCheckpointCommand(
                    ExpectedCheckpointVersion: 0,
                    DataRightsRestoreCursor.Genesis,
                    new DataRightsLedgerDeltaCursor(
                        TenantSequence: 1,
                        EntrySha256: new string('f', 64),
                        StorageMacSha256: new string('c', 64)),
                    [proof]),
                CancellationToken.None);

        Assert.Equal(
            "DataRights.RestoreOwnerProofInvalid",
            invalid.Error.Code);
        Assert.Null(checkpoints.Checkpoint);

        Result<DataRightsRestoreCheckpointState> completed =
            await advance.HandleAsync(
                new AdvanceDataRightsRestoreCheckpointCommand(
                    ExpectedCheckpointVersion: 0,
                    DataRightsRestoreCursor.Genesis,
                    new DataRightsLedgerDeltaCursor(
                        TenantSequence: 1,
                        ledger.EntrySha256,
                        StorageMacSha256: new string('c', 64)),
                    [proof]),
                CancellationToken.None);

        Assert.True(completed.IsSuccess);
        Assert.Equal(1, completed.Value.Version);
        Assert.Equal(1, completed.Value.Cursor.TenantSequence);
        Assert.Equal(ledger.EntrySha256, completed.Value.Cursor.EntrySha256);
    }

    private sealed class RecordingCheckpointRepository
        : IDataRightsRestoreCheckpointRepository
    {
        public DataRightsRestoreCheckpoint? Checkpoint { get; private set; }

        public Task<DataRightsRestoreCheckpoint?> GetAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Checkpoint);

        public Task AddAsync(
            DataRightsRestoreCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            this.Checkpoint = checkpoint;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLedgerRepository
        : IDataRightsProcessingLedgerRepository
    {
        public List<DataRightsProcessingLedgerEntry> Entries { get; } = [];

        public Task AddAsync(
            DataRightsProcessingLedgerEntry entry,
            CancellationToken cancellationToken)
        {
            this.Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<DataRightsProcessingLedgerEntry?> GetByWorkItemAsync(
            Guid workItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Entries.SingleOrDefault(
                    entry => entry.WorkItemId == workItemId));

        public Task<DataRightsProcessingLedgerEntry?> GetByOwnerReceiptAsync(
            string ownerKey,
            Guid ownerReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Entries.SingleOrDefault(
                    entry =>
                        entry.OwnerKey == ownerKey &&
                        entry.OwnerReceiptId == ownerReceiptId));

        public Task<DataRightsProcessingLedgerEntry?> GetLatestAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Entries.LastOrDefault());

        public Task<DataRightsProcessingLedgerEntry?> GetBySequenceAsync(
            long tenantSequence,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Entries.SingleOrDefault(
                    entry => entry.TenantSequence == tenantSequence));
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
