namespace BunkFy.Modules.Ingestion.Application.Policies;

using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Results;

internal static class IngestionTenantLifecycleAdmission
{
    public static async ValueTask<Result> AuthorizeAsync(
        IEnumerable<IIngestionTenantLifecyclePolicy>? policies,
        string tenantId,
        IngestionTenantLifecycleOperation operation,
        CancellationToken cancellationToken)
    {
        IngestionTenantLifecycleOutcome outcome = await EvaluateAsync(
            policies,
            tenantId,
            operation,
            cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            IngestionTenantLifecycleOutcome.Allowed => Result.Success(),
            IngestionTenantLifecycleOutcome.Restricted => Result.Failure(
                IngestionApplicationErrors.TenantLifecycleRestricted),
            _ => Result.Failure(
                IngestionApplicationErrors
                    .TenantLifecycleAdmissionUnavailable)
        };
    }

    public static async ValueTask<IngestionTenantLifecycleOutcome>
        EvaluateAsync(
            IEnumerable<IIngestionTenantLifecyclePolicy>? policies,
            string tenantId,
            IngestionTenantLifecycleOperation operation,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId) ||
            operation == IngestionTenantLifecycleOperation.Unknown)
        {
            return IngestionTenantLifecycleOutcome.Unavailable;
        }

        foreach (IIngestionTenantLifecyclePolicy policy in policies ?? [])
        {
            IngestionTenantLifecycleDecision decision;
            try
            {
                decision = await policy.AuthorizeAsync(
                        tenantId,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                return IngestionTenantLifecycleOutcome.Unavailable;
            }

            if (decision.Outcome ==
                IngestionTenantLifecycleOutcome.Restricted)
            {
                return IngestionTenantLifecycleOutcome.Restricted;
            }

            if (decision.Outcome !=
                IngestionTenantLifecycleOutcome.Allowed)
            {
                return IngestionTenantLifecycleOutcome.Unavailable;
            }
        }

        return IngestionTenantLifecycleOutcome.Allowed;
    }
}
