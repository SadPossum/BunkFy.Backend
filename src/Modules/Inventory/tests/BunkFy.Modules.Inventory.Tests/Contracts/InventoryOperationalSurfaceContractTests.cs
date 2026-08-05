namespace BunkFy.Modules.Inventory.Tests.Contracts;

using BunkFy.Modules.Inventory.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryOperationalSurfaceContractTests
{
    [Fact]
    public void Directory_contracts_publish_truthful_continuation_metadata()
    {
        AssertProperties<RoomInventoryListResponse>("HasMore", "Page", "PageSize", "Rooms");
        AssertProperties<ManualInventoryBlockListResponse>("Blocks", "HasMore", "Page", "PageSize");
    }

    [Fact]
    public void Ordinary_mutation_receipts_remain_minimal()
    {
        AssertProperties<RoomInventoryMutationReceiptDto>(
            "PropertyId",
            "RoomId",
            "SalesMode",
            "Version");
        AssertProperties<ManualInventoryBlockMutationReceiptDto>(
            "BlockGroupId",
            "BlockId",
            "PropertyId",
            "Status",
            "Version");
        AssertProperties<ManualInventoryBlockGroupMutationReceiptDto>(
            "AffectedBlockCount",
            "BlockGroupId",
            "PropertyId");
    }

    private static void AssertProperties<T>(params string[] expected)
    {
        string[] actual = typeof(T)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
