namespace BunkFy.Modules.DataRights.Tests;

using BunkFy.Modules.DataRights.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsExportRecordIdsTests
{
    private static readonly Guid NamespaceId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public void Deterministic_child_is_stable_and_uses_uuid_version_8()
    {
        Guid first = DataRightsExportRecordIds.CreateDeterministicChild(
            NamespaceId,
            "child-1");
        Guid second = DataRightsExportRecordIds.CreateDeterministicChild(
            NamespaceId,
            "child-1");
        string canonical = first.ToString("D");

        Assert.Equal(first, second);
        Assert.Equal('8', canonical[14]);
        Assert.Contains(canonical[19], "89ab");
    }

    [Fact]
    public void Deterministic_child_separates_namespaces_and_discriminators()
    {
        Guid baseline = DataRightsExportRecordIds.CreateDeterministicChild(
            NamespaceId,
            "child-1");

        Assert.NotEqual(
            baseline,
            DataRightsExportRecordIds.CreateDeterministicChild(
                NamespaceId,
                "child-2"));
        Assert.NotEqual(
            baseline,
            DataRightsExportRecordIds.CreateDeterministicChild(
                Guid.Parse("10000000-0000-0000-0000-000000000002"),
                "child-1"));
    }

    [Fact]
    public void Deterministic_child_rejects_invalid_coordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DataRightsExportRecordIds.CreateDeterministicChild(
                Guid.Empty,
                "child-1"));
        Assert.Throws<ArgumentException>(() =>
            DataRightsExportRecordIds.CreateDeterministicChild(
                NamespaceId,
                " "));
    }
}
