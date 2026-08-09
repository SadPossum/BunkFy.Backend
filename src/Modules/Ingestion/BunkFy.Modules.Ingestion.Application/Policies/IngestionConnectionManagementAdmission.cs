namespace BunkFy.Modules.Ingestion.Application.Policies;

using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal static class IngestionConnectionManagementAdmission
{
    public static async ValueTask<Result<string>> AuthorizeAsync(
        IScopeContext scopeContext,
        IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<string>(
                IngestionApplicationErrors.ScopeRequired);
        }

        if (operationId == Guid.Empty)
        {
            return Result.Failure<string>(
                IngestionApplicationErrors
                    .ConnectionManagementOperationInvalid);
        }

        Result lifecycleAdmission =
            await IngestionTenantLifecycleAdmission.AuthorizeAsync(
            lifecyclePolicies,
            scopeContext.ScopeId,
            IngestionTenantLifecycleOperation.ConnectionProvisioning,
            cancellationToken).ConfigureAwait(false);
        return lifecycleAdmission.IsSuccess
            ? Result.Success(scopeContext.ScopeId)
            : Result.Failure<string>(lifecycleAdmission.Error);
    }
}
