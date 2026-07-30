namespace BunkFy.Modules.Staff.Application.Policies;

using BunkFy.Modules.Staff.Contracts;

internal sealed class StaffRetentionPrerequisiteEvaluator(
    IEnumerable<IStaffRetentionAnonymisationPrerequisite> contributors)
{
    private readonly IStaffRetentionAnonymisationPrerequisite[] ordered =
        contributors
            .OrderBy(
                contributor => contributor.ContributorKey,
                StringComparer.Ordinal)
            .ToArray();

    public async Task<StaffRetentionPrerequisiteEvaluation> EvaluateAsync(
        StaffRetentionAnonymisationPrerequisiteRequest request,
        CancellationToken cancellationToken)
    {
        if (this.ordered.Length == 0 ||
            this.ordered.Any(contributor =>
                !IsCode(
                    contributor.ContributorKey,
                    StaffRetentionAnonymisationPrerequisiteContract
                        .ContributorKeyMaxLength)) ||
            this.ordered
                .GroupBy(
                    contributor => contributor.ContributorKey,
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            return StaffRetentionPrerequisiteEvaluation.Unavailable;
        }

        foreach (IStaffRetentionAnonymisationPrerequisite contributor in
                 this.ordered)
        {
            StaffRetentionAnonymisationPrerequisiteResult result;
            try
            {
                result = await contributor.ExecuteAsync(
                    request,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                return StaffRetentionPrerequisiteEvaluation.Unavailable;
            }

            if (result is null ||
                result.ContractVersion !=
                    StaffRetentionAnonymisationPrerequisiteContract
                        .CurrentVersion ||
                !IsCode(
                    result.OutcomeCode,
                    StaffRetentionAnonymisationPrerequisiteContract
                        .OutcomeCodeMaxLength))
            {
                return StaffRetentionPrerequisiteEvaluation.Unavailable;
            }

            if (result.Status ==
                StaffRetentionAnonymisationPrerequisiteStatus.Blocked)
            {
                return StaffRetentionPrerequisiteEvaluation.Blocked;
            }

            if (result.Status !=
                StaffRetentionAnonymisationPrerequisiteStatus.Completed)
            {
                return StaffRetentionPrerequisiteEvaluation.Unavailable;
            }
        }

        return StaffRetentionPrerequisiteEvaluation.Completed;
    }

    private static bool IsCode(string? value, int maxLength)
    {
        string code = value?.Trim() ?? string.Empty;
        return code.Length is > 0 &&
            code.Length <= maxLength &&
            code.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '.');
    }
}

internal enum StaffRetentionPrerequisiteEvaluation
{
    Completed = 1,
    Blocked = 2,
    Unavailable = 3
}
