namespace BunkFy.Modules.Retention.Application;

using BunkFy.Modules.Retention.Contracts;

internal static class RetentionContributorCatalog
{
    public static RetentionScheduleDescriptor[] GetDescriptors(
        IEnumerable<IRetentionExecutionContributor> contributors)
    {
        RetentionScheduleDescriptor[] descriptors = contributors
            .Select(contributor => contributor.Schedule)
            .OrderBy(descriptor => descriptor.ContributorKey, StringComparer.Ordinal)
            .ToArray();
        if (descriptors
            .GroupBy(descriptor => descriptor.ContributorKey, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                "Retention.ContributorDescriptorDuplicate");
        }

        return descriptors;
    }
}
