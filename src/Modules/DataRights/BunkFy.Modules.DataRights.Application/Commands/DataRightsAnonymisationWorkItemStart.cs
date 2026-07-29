namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;

internal sealed record DataRightsAnonymisationWorkItemStart(
    bool DispatchRequired,
    long WorkItemVersion,
    DataRightsAnonymisationContributionRequest? PropertyRequest,
    DataRightsAnonymisationContributionRequestV2? ScopedRequest)
{
    public static DataRightsAnonymisationWorkItemStart Terminal(long workItemVersion) =>
        new(
            false,
            workItemVersion,
            PropertyRequest: null,
            ScopedRequest: null);

    public static DataRightsAnonymisationWorkItemStart Ready(
        long workItemVersion,
        DataRightsAnonymisationContributionRequest request) =>
        new(
            true,
            workItemVersion,
            request,
            ScopedRequest: null);

    public static DataRightsAnonymisationWorkItemStart Ready(
        long workItemVersion,
        DataRightsAnonymisationContributionRequestV2 request) =>
        new(
            true,
            workItemVersion,
            PropertyRequest: null,
            request);
}
