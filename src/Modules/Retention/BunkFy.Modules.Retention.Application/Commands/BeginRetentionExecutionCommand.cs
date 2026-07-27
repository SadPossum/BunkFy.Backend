namespace BunkFy.Modules.Retention.Application.Commands;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;

public sealed record BeginRetentionExecutionCommand(
    Guid ExecutionId,
    string TenantId,
    string OwnerKey,
    string DataClassKey,
    RetentionTargetScopeKind TargetScopeKind,
    Guid? PropertyId,
    int ExecutionPolicyVersion,
    int Attempt,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineUtc,
    DateTimeOffset NextDueAtUtc)
    : ITransactionalCommand<RetentionExecutionStart>;
