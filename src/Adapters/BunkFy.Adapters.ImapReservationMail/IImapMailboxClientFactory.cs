namespace BunkFy.Adapters.ImapReservationMail;

internal interface IImapMailboxClientFactory
{
    Task<IImapMailboxSession> OpenAsync(
        ImapReservationMailSettings settings,
        ImapCredential credential,
        CancellationToken cancellationToken);
}

internal interface IImapMailboxSession : IAsyncDisposable
{
    uint UidValidity { get; }

    Task<ImapMailboxMessageSummary?> GetNextAsync(
        uint afterUid,
        CancellationToken cancellationToken);

    Task<byte[]> ReadMessageAsync(
        uint uid,
        int maximumBytes,
        CancellationToken cancellationToken);
}

internal sealed record ImapMailboxMessageSummary(
    uint Uid,
    long Size,
    DateTimeOffset? InternalDateUtc);
