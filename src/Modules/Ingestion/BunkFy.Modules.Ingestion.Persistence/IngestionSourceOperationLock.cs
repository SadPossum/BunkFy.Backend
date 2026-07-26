namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Domain.Models;

internal sealed class IngestionSourceOperationLock : ScopedEntity<Guid>
{
    private IngestionSourceOperationLock() { }

    public IngestionSourceOperationLock(
        Guid id,
        string scopeId,
        Guid sourceLinkId)
        : base(id, scopeId)
    {
        if (id == Guid.Empty || sourceLinkId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Ingestion source operation-lock coordinate is invalid.");
        }

        this.SourceLinkId = sourceLinkId;
    }

    public Guid SourceLinkId { get; private set; }
    public long Revision { get; private set; } = 1;

    public void Touch() =>
        this.Revision = checked(this.Revision + 1);
}
