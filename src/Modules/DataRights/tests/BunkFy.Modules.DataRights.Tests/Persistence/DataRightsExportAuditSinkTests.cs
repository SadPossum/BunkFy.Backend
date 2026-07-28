namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.DataRights.Persistence.Repositories;
using BunkFy.Modules.DataRights.Persistence.Security;
using Gma.Framework.Observability;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsExportAuditSinkTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(
        DataRightsExportAuditAction.GenerationCompleted,
        "available",
        "data-rights.export-generation-completed")]
    [InlineData(
        DataRightsExportAuditAction.GenerationFailed,
        "storage-failed",
        "data-rights.export-generation-failed")]
    [InlineData(
        DataRightsExportAuditAction.Download,
        "succeeded",
        "data-rights.export-download-completed")]
    [InlineData(DataRightsExportAuditAction.Download, "expired", null)]
    [InlineData(DataRightsExportAuditAction.GenerationRequested, "requested", null)]
    public void Audit_facts_map_only_to_finite_sensitive_export_signals(
        DataRightsExportAuditAction action,
        string outcomeCode,
        string? expectedCode)
    {
        SecuritySignalDefinition? definition =
            DataRightsExportSecuritySignalDefinitions.ForAudit(
                action,
                outcomeCode);

        Assert.Equal(expectedCode, definition?.Code);
    }

    [Fact]
    public async Task Signal_is_recorded_only_after_the_audit_entry_is_durable()
    {
        DbContextOptions<DataRightsDbContext> options =
            new DbContextOptionsBuilder<DataRightsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using DataRightsDbContext dbContext = new(
            options,
            new TestScopeContext("tenant-a"));
        RecordingSecuritySignalRecorder securitySignals = new(() =>
            dbContext.ChangeTracker
                .Entries<DataRightsExportAuditEntry>()
                .All(entry => entry.State == EntityState.Unchanged));
        DataRightsExportAuditSink sink = new(
            dbContext,
            new FixedIdGenerator(Guid.NewGuid()),
            securitySignals);

        await sink.RecordAsync(
            new(
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                DataRightsCaseType.GuestRights,
                Guid.NewGuid(),
                DataRightsExportAuditAction.GenerationFailed,
                "system:data-rights-export",
                "storage-failed",
                Now),
            CancellationToken.None);

        Assert.True(securitySignals.WasDurableAtEmission);
        Assert.Equal(
            "data-rights.export-generation-failed",
            Assert.Single(securitySignals.Definitions).Code);
        Assert.Equal(1, await dbContext.ExportAuditEntries.CountAsync());
    }

    private sealed class RecordingSecuritySignalRecorder(
        Func<bool> isDurable)
        : ISecuritySignalRecorder
    {
        public List<SecuritySignalDefinition> Definitions { get; } = [];
        public bool WasDurableAtEmission { get; private set; }

        public SecuritySignalReceipt Record(
            SecuritySignalDefinition definition,
            Guid? correlationId = null)
        {
            this.WasDurableAtEmission = isDurable();
            this.Definitions.Add(definition);
            return new(
                correlationId?.ToString("N") ?? new string('0', 32),
                true);
        }
    }

    private sealed class FixedIdGenerator(Guid id) : IIdGenerator
    {
        public Guid NewId() => id;
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
