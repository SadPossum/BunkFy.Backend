namespace BunkFy.Modules.Inventory.Application.Policies;

using System.Security.Cryptography;
using System.Text;

internal static class InventoryAnonymisationIdentity
{
    private const string ReservationLabel =
        "inventory-allocation-reservation";

    public static Guid CreateReservationPseudonym(
        Guid ownerReceiptId)
    {
        if (ownerReceiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "An owner receipt id is required.",
                nameof(ownerReceiptId));
        }

        byte[] digest = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                $"{ReservationLabel}:{ownerReceiptId:N}"));
        return Guid.ParseExact(
            Convert.ToHexStringLower(digest.AsSpan(0, 16)),
            "N");
    }
}
