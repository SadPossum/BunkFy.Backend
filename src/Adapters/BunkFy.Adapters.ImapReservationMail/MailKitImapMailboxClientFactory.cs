namespace BunkFy.Adapters.ImapReservationMail;

using System.Buffers;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

internal sealed class MailKitImapMailboxClientFactory : IImapMailboxClientFactory
{
    public async Task<IImapMailboxSession> OpenAsync(
        ImapReservationMailSettings settings,
        ImapCredential credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credential);

        ImapClient client = new()
        {
            Timeout = checked((int)settings.NetworkTimeout.TotalMilliseconds)
        };
        try
        {
            await client.ConnectAsync(
                settings.Host,
                settings.Port,
                settings.SocketOptions,
                cancellationToken).ConfigureAwait(false);
            if (credential.Authentication == ImapAuthenticationKind.OAuth2)
            {
                await client.AuthenticateAsync(
                    new SaslMechanismOAuth2(credential.Username, credential.Credential),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await client.AuthenticateAsync(
                    credential.Username,
                    credential.Credential,
                    cancellationToken).ConfigureAwait(false);
            }

            IMailFolder folder = await client.GetFolderAsync(
                settings.Mailbox,
                cancellationToken).ConfigureAwait(false);
            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
            return new MailKitImapMailboxSession(client, folder);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private sealed class MailKitImapMailboxSession(ImapClient client, IMailFolder folder)
        : IImapMailboxSession
    {
        private const int CopyBufferBytes = 64 * 1024;
        private bool disposed;

        public uint UidValidity => folder.UidValidity;

        public async Task<ImapMailboxMessageSummary?> GetNextAsync(
            uint afterUid,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            int count = folder.Count;
            if (afterUid == uint.MaxValue || count == 0)
            {
                return null;
            }

            int low = 0;
            int high = count - 1;
            IMessageSummary? candidate = null;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                IMessageSummary summary = await FetchAtAsync(
                    folder,
                    middle,
                    cancellationToken).ConfigureAwait(false);
                if (summary.UniqueId.Id <= afterUid)
                {
                    low = middle + 1;
                }
                else
                {
                    candidate = summary;
                    high = middle - 1;
                }
            }

            if (candidate is null)
            {
                return null;
            }

            if (candidate.Size is null)
            {
                throw new InvalidOperationException("The IMAP server did not return the requested message size.");
            }

            return new(
                candidate.UniqueId.Id,
                candidate.Size.Value,
                candidate.InternalDate?.ToUniversalTime());
        }

        public async Task<byte[]> ReadMessageAsync(
            uint uid,
            int maximumBytes,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            ArgumentOutOfRangeException.ThrowIfZero(uid);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

            await using Stream source = await folder.GetStreamAsync(
                new UniqueId(folder.UidValidity, uid),
                cancellationToken).ConfigureAwait(false);
            using MemoryStream destination = new(Math.Min(maximumBytes, CopyBufferBytes));
            byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferBytes);
            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(
                        buffer.AsMemory(0, CopyBufferBytes),
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    if (destination.Length + read > maximumBytes)
                    {
                        throw new InvalidOperationException(
                            "The IMAP message exceeded its advertised bounded size.");
                    }

                    await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken).ConfigureAwait(false);
                }

                return destination.ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            try
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(quit: true).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is IOException or ImapProtocolException or ImapCommandException)
            {
                // Source processing is already complete; disconnect failure cannot revoke a durable acknowledgement.
            }
            finally
            {
                client.Dispose();
            }
        }

        private static async Task<IMessageSummary> FetchAtAsync(
            IMailFolder folder,
            int index,
            CancellationToken cancellationToken)
        {
            IList<IMessageSummary> summaries = await folder.FetchAsync(
                index,
                index,
                new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Size |
                    MessageSummaryItems.InternalDate),
                cancellationToken).ConfigureAwait(false);
            return summaries.Count == 1 && summaries[0].UniqueId.IsValid
                ? summaries[0]
                : throw new InvalidOperationException(
                    "The IMAP folder changed while locating the next message.");
        }
    }
}
