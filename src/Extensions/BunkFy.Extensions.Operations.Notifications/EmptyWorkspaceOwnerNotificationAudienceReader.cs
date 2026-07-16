namespace BunkFy.Extensions.Operations.Notifications;

internal sealed class EmptyWorkspaceOwnerNotificationAudienceReader
    : IWorkspaceOwnerNotificationAudienceReader
{
    public Task<IReadOnlyList<string>> ListAuthSubjectIdsAsync(
        string scopeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
