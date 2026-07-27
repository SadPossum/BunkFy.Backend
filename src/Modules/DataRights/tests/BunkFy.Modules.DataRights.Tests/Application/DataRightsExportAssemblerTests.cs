namespace BunkFy.Modules.DataRights.Tests.Application;

using System.Text.Json;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsExportAssemblerTests
{
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Assembly_is_deterministic_and_orders_subjects_and_fields()
    {
        IDataRightsSubjectExportContributor contributor =
            new SuccessfulContributor();
        DataRightsExportAssembler assembler = new([contributor]);
        DataRightsExportGenerationRequest request = Request([
            new("staff", "profile", Guid.Parse(
                "22222222-2222-2222-2222-222222222222"), 3),
            new("staff", "profile", Guid.Parse(
                "11111111-1111-1111-1111-111111111111"), 2)
        ]);

        await using MemoryStream first = new();
        await using MemoryStream second = new();
        DataRightsExportAssemblyResult result = await assembler.AssembleAsync(
            request,
            first,
            CancellationToken.None);
        _ = await assembler.AssembleAsync(
            request,
            second,
            CancellationToken.None);

        Assert.Equal(first.ToArray(), second.ToArray());
        Assert.Equal(2, result.SubjectCount);
        Assert.Equal(2, result.RecordCount);
        using JsonDocument document = JsonDocument.Parse(first.ToArray());
        JsonElement root = document.RootElement;
        Assert.Equal("staffRights", root.GetProperty("caseType").GetString());
        Assert.Equal(
            GeneratedAt.AddHours(24),
            root.GetProperty("expiresAtUtc").GetDateTimeOffset());
        JsonElement.ArrayEnumerator subjects =
            root.GetProperty("subjects").EnumerateArray();
        Assert.True(subjects.MoveNext());
        Assert.Equal(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            subjects.Current.GetProperty("coordinate")
                .GetProperty("recordId").GetGuid());
    }

    [Fact]
    public async Task Assembly_rejects_partial_stale_owner_output()
    {
        DataRightsExportAssembler assembler = new([
            new StaleContributor()
        ]);
        await using MemoryStream destination = new();

        DataRightsExportGenerationException exception =
            await Assert.ThrowsAsync<DataRightsExportGenerationException>(
                () => assembler.AssembleAsync(
                    Request([
                        new("staff", "profile", Guid.NewGuid(), 2)
                    ]),
                    destination,
                    CancellationToken.None));

        Assert.Equal("subject-stale", exception.Code);
    }

    [Fact]
    public async Task Assembly_rejects_duplicate_owner_contributors()
    {
        DataRightsExportAssembler assembler = new([
            new SuccessfulContributor(),
            new SuccessfulContributor()
        ]);
        await using MemoryStream destination = new();

        DataRightsExportGenerationException exception =
            await Assert.ThrowsAsync<DataRightsExportGenerationException>(
                () => assembler.AssembleAsync(
                    Request([
                        new("staff", "profile", Guid.NewGuid(), 2)
                    ]),
                    destination,
                    CancellationToken.None));

        Assert.Equal("owner-unavailable", exception.Code);
    }

    private static DataRightsExportGenerationRequest Request(
        IReadOnlyCollection<DataRightsSubjectCoordinate> subjects) => new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "tenant-a",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            DataRightsCaseType.StaffRights,
            PropertyId: null,
            DecisionRevision: 7,
            subjects,
            GeneratedAt,
            GeneratedAt.AddHours(24));

    private class SuccessfulContributor : IDataRightsSubjectExportContributor
    {
        public string OwnerKey => "staff";

        public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes =>
            [DataRightsCaseType.StaffRights];

        public DataRightsExportDescriptor Descriptor => new(
            "staff",
            "staff.catalog",
            CatalogSchemaVersion: 1,
            CatalogVersion: 2,
            "staff.export",
            ExportSchemaVersion: 1,
            ["name", "email"]);

        public virtual async Task<DataRightsSubjectExportResult> ExportAsync(
            DataRightsSubjectExportRequest request,
            IDataRightsExportSink sink,
            CancellationToken cancellationToken)
        {
            await sink.WriteAsync(
                new DataRightsExportRecord(
                    "profile",
                    request.Coordinate.RecordId,
                    request.Coordinate.RecordVersion,
                    [
                        new("name", JsonSerializer.SerializeToElement("Artem")),
                        new("email", JsonSerializer.SerializeToElement(
                            "user@example.test"))
                    ]),
                cancellationToken);
            return DataRightsSubjectExportResult.Success(1);
        }
    }

    private sealed class StaleContributor : SuccessfulContributor
    {
        public override async Task<DataRightsSubjectExportResult> ExportAsync(
            DataRightsSubjectExportRequest request,
            IDataRightsExportSink sink,
            CancellationToken cancellationToken)
        {
            _ = await base.ExportAsync(request, sink, cancellationToken);
            return DataRightsSubjectExportResult.Stale();
        }
    }
}
