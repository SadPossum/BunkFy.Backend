namespace BunkFy.Modules.Retention.Application.Tasks;

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Tasks;

internal sealed class RetentionScheduleProvider(
    IRetentionScopeRepository scopes,
    IEnumerable<IRetentionExecutionContributor> contributors)
    : ITaskScheduleProvider
{
    public async IAsyncEnumerable<ScheduledTaskDefinition> GetSchedulesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RetentionScheduleDescriptor[] descriptors =
            RetentionContributorCatalog.GetDescriptors(contributors);

        foreach (IGrouping<RetentionTargetScopeKind, RetentionScheduleDescriptor> group in
                 descriptors.GroupBy(descriptor => descriptor.TargetScopeKind))
        {
            await foreach (RetentionScheduleTarget target in scopes
                .StreamActiveTargetsAsync(group.Key, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                foreach (RetentionScheduleDescriptor descriptor in group)
                {
                    ExecuteRetentionSchedulePayload payload = new(
                        descriptor.OwnerKey,
                        descriptor.DataClassKey,
                        descriptor.ExecutionPolicyVersion,
                        descriptor.TargetScopeKind,
                        target.PropertyId);
                    yield return new ScheduledTaskDefinition(
                        CreateScheduleName(descriptor, target),
                        RetentionModuleMetadata.Name,
                        ExecuteRetentionSchedulePayload.TaskName,
                        JsonSerializer.Serialize(payload),
                        descriptor.Interval,
                        RetentionModuleMetadata.WorkerGroup,
                        target.ScopeId,
                        descriptor.MaxAttempts,
                        ExecuteRetentionSchedulePayload.PayloadVersion,
                        runOnStart: true);
                }
            }
        }
    }

    private static string CreateScheduleName(
        RetentionScheduleDescriptor descriptor,
        RetentionScheduleTarget target)
    {
        string coordinate =
            $"{target.ScopeId}|{target.PropertyId:N}|{descriptor.ContributorKey}|" +
            $"{(int)descriptor.TargetScopeKind}";
        string digest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(coordinate)))[..16];
        return $"ret-{Take(descriptor.OwnerKey)}-{Take(descriptor.DataClassKey)}-" +
            $"v{descriptor.ExecutionPolicyVersion}-{digest}";
    }

    private static string Take(string value) =>
        value.Length <= 24 ? value : value[..24];
}
