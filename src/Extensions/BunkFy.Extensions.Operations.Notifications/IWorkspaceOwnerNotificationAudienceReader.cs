namespace BunkFy.Extensions.Operations.Notifications;

internal interface IWorkspaceOwnerNotificationAudienceReader
{
    Task<IReadOnlyList<string>> ListAuthSubjectIdsAsync(
        string scopeId,
        CancellationToken cancellationToken);
}
