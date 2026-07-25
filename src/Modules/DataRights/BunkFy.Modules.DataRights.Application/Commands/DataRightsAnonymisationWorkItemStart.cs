namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;

internal sealed record DataRightsAnonymisationWorkItemStart(
    bool DispatchRequired,
    long WorkItemVersion,
    DataRightsAnonymisationContributionRequest? Request)
{
    public static DataRightsAnonymisationWorkItemStart Terminal(long workItemVersion) =>
        new(false, workItemVersion, Request: null);

    public static DataRightsAnonymisationWorkItemStart Ready(
        long workItemVersion,
        DataRightsAnonymisationContributionRequest request) =>
        new(true, workItemVersion, request);
}
