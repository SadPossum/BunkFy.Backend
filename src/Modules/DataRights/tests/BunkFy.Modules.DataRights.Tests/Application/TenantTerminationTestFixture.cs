namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

internal static class TenantTerminationTestFixture
{
    public static Result CompleteFreeze(
        TenantTerminationProcess process,
        long operationRevision,
        long expectedVersion,
        string actorId,
        DateTimeOffset frozenAtUtc,
        string? frozenRevisionSha256 = null,
        IReadOnlyCollection<TenantTerminationFrozenOwnerDescriptor>?
            frozenOwners = null,
        long workspaceFenceRevision = 3) =>
        process.CompleteFreeze(
            operationRevision,
            workspaceFenceRevision,
            frozenRevisionSha256 ?? process.PolicyEvidenceSha256,
            frozenOwners ??
            [
                new(
                    "workspaces",
                    1,
                    1,
                    process.PolicyEvidenceSha256)
            ],
            expectedVersion,
            actorId,
            frozenAtUtc);
}
