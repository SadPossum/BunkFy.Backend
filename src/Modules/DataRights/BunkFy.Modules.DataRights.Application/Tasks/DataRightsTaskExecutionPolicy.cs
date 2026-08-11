namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Contracts;

internal static class DataRightsTaskExecutionPolicy
{
    public static TimeSpan CompletionGrace { get; } =
        TimeSpan.FromMinutes(1);

    public static TimeSpan AnonymisationOwnerHandlerTimeout { get; } =
        BeginDataRightsAnonymisationWorkItemCommandHandler.OwnerCallTimeout +
        CompletionGrace;

    public static TimeSpan TenantTerminationOwnerHandlerTimeout { get; } =
        BeginTenantTerminationOwnerWorkCommandHandler.OwnerCallTimeout +
        CompletionGrace;

    public static TimeSpan TenantTerminationVerificationHandlerTimeout { get; } =
        TimeSpan.FromTicks(checked(
            BeginTenantTerminationOwnerWorkCommandHandler.OwnerCallTimeout.Ticks *
            TenantTerminationContract.MaximumContributors)) +
        CompletionGrace;
}
