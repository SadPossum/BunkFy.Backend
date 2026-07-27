namespace BunkFy.Modules.Retention.Application.Tasks;

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
    public async Task<IReadOnlyList<ScheduledTaskDefinition>> GetSchedulesAsync(
        CancellationToken cancellationToken)
    {
        RetentionScheduleDescriptor[] descriptors = contributors
            .Select(contributor => contributor.Schedule)
            .OrderBy(schedule => schedule.ContributorKey, StringComparer.Ordinal)
            .ToArray();
        EnsureUnique(descriptors);

        List<ScheduledTaskDefinition> schedules = [];
        foreach (IGrouping<RetentionTargetScopeKind, RetentionScheduleDescriptor> group in
                 descriptors.GroupBy(descriptor => descriptor.TargetScopeKind))
        {
            IReadOnlyList<RetentionScheduleTarget> targets =
                await scopes.ListActiveTargetsAsync(
                    group.Key,
                    cancellationToken).ConfigureAwait(false);
            foreach (RetentionScheduleDescriptor descriptor in group)
            {
                foreach (RetentionScheduleTarget target in targets)
                {
                    ExecuteRetentionSchedulePayload payload = new(
                        descriptor.OwnerKey,
                        descriptor.DataClassKey,
                        descriptor.ExecutionPolicyVersion,
                        descriptor.TargetScopeKind,
                        target.PropertyId);
                    schedules.Add(new ScheduledTaskDefinition(
                        CreateScheduleName(descriptor, target),
                        RetentionModuleMetadata.Name,
                        ExecuteRetentionSchedulePayload.TaskName,
                        JsonSerializer.Serialize(payload),
                        descriptor.Interval,
                        RetentionModuleMetadata.WorkerGroup,
                        target.ScopeId,
                        descriptor.MaxAttempts,
                        ExecuteRetentionSchedulePayload.PayloadVersion,
                        runOnStart: true));
                }
            }
        }

        return schedules;
    }

    private static void EnsureUnique(
        IReadOnlyCollection<RetentionScheduleDescriptor> descriptors)
    {
        bool duplicate = descriptors
            .GroupBy(descriptor => descriptor.ContributorKey, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        if (duplicate)
        {
            throw new InvalidOperationException(
                "Retention.ContributorDescriptorDuplicate");
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
