namespace BunkFy.Modules.Retention.Persistence;

using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class RetentionMutationLock(RetentionDbContext dbContext)
    : IRetentionMutationLock
{
    private const string TenantTargetPrefix =
        "bunkfy:retention:target:tenant:";
    private const string PropertyTargetPrefix =
        "bunkfy:retention:target:property:";
    private const string ExecutionPrefix = "bunkfy:retention:execution:";
    private const string SchedulePrefix = "bunkfy:retention:schedule:";

    public Task AcquireTenantTargetReadAsync(
        string tenantId,
        CancellationToken cancellationToken) => this.AcquireTenantTargetAsync(
        tenantId,
        EfTransactionKeyLockMode.Shared,
        cancellationToken);

    public Task AcquireTenantTargetWriteAsync(
        string tenantId,
        CancellationToken cancellationToken) => this.AcquireTenantTargetAsync(
        tenantId,
        EfTransactionKeyLockMode.Exclusive,
        cancellationToken);

    public Task AcquirePropertyTargetReadAsync(
        string tenantId,
        Guid propertyId,
        CancellationToken cancellationToken) => this.AcquireResourceAsync(
        PropertyTargetPrefix,
        this.ValidateTenant(tenantId),
        ValidateId(propertyId, "property"),
        EfTransactionKeyLockMode.Shared,
        cancellationToken);

    public Task AcquirePropertyTargetWriteAsync(
        string tenantId,
        Guid propertyId,
        CancellationToken cancellationToken) => this.AcquireResourceAsync(
        PropertyTargetPrefix,
        this.ValidateTenant(tenantId),
        ValidateId(propertyId, "property"),
        EfTransactionKeyLockMode.Exclusive,
        cancellationToken);

    public Task AcquireExecutionAsync(
        string tenantId,
        Guid executionId,
        CancellationToken cancellationToken) => this.AcquireResourceAsync(
        ExecutionPrefix,
        this.ValidateTenant(tenantId),
        ValidateId(executionId, "execution"),
        EfTransactionKeyLockMode.Exclusive,
        cancellationToken);

    public Task AcquireScheduleAsync(
        string tenantId,
        string ownerKey,
        string dataClassKey,
        Guid? propertyId,
        int executionPolicyVersion,
        CancellationToken cancellationToken)
    {
        string scopeId = this.ValidateTenant(tenantId);
        string owner = NormalizeKey(ownerKey);
        string dataClass = NormalizeKey(dataClassKey);
        if (executionPolicyVersion <= 0 ||
            propertyId == Guid.Empty ||
            owner.Length == 0 ||
            dataClass.Length == 0)
        {
            throw new InvalidOperationException(
                "A Retention schedule lock requires valid coordinates.");
        }

        string target = propertyId?.ToString("N") ?? "tenant";
        string resource = SchedulePrefix + scopeId + ':' + owner + ':' +
            dataClass + ':' + target + ':' + executionPolicyVersion;
        return this.AcquireAsync(
            resource,
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);
    }

    private Task AcquireTenantTargetAsync(
        string tenantId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken) => this.AcquireAsync(
        TenantTargetPrefix + this.ValidateTenant(tenantId),
        mode,
        cancellationToken);

    private Task AcquireResourceAsync(
        string prefix,
        string tenantId,
        Guid id,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken) => this.AcquireAsync(
        prefix + tenantId + ':' + id.ToString("N"),
        mode,
        cancellationToken);

    private Task AcquireAsync(
        string resource,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return Task.CompletedTask;
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Retention mutation lock requires an active database transaction.");
        }

        return EfTransactionKeyLock.AcquireAsync(
            dbContext,
            resource,
            mode,
            cancellationToken);
    }

    private string ValidateTenant(string tenantId)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            !string.Equals(
                scopeId,
                dbContext.CurrentScopeId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A Retention mutation lock requires the current tenant scope.");
        }

        return scopeId;
    }

    private static Guid ValidateId(Guid id, string coordinate)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"A Retention {coordinate} lock requires a valid coordinate.");
        }

        return id;
    }

    private static string NormalizeKey(string value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length is
                > 0 and <= RetentionExecutionContract.KeyMaxLength &&
            normalized.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '.')
            ? normalized
            : string.Empty;
    }
}
