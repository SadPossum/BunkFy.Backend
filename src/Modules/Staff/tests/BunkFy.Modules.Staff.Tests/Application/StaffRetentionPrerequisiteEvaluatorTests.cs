namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Application.Policies;
using BunkFy.Modules.Staff.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffRetentionPrerequisiteEvaluatorTests
{
    [Fact]
    public async Task Missing_contributor_fails_closed()
    {
        StaffRetentionPrerequisiteEvaluation result =
            await new StaffRetentionPrerequisiteEvaluator([])
                .EvaluateAsync(Request(), CancellationToken.None);

        Assert.Equal(
            StaffRetentionPrerequisiteEvaluation.Unavailable,
            result);
    }

    [Fact]
    public async Task Duplicate_contributor_key_fails_closed()
    {
        StaffRetentionPrerequisiteEvaluation result =
            await new StaffRetentionPrerequisiteEvaluator(
                [
                    new StubPrerequisite("workspace-access"),
                    new StubPrerequisite("workspace-access")
                ])
                .EvaluateAsync(Request(), CancellationToken.None);

        Assert.Equal(
            StaffRetentionPrerequisiteEvaluation.Unavailable,
            result);
    }

    [Fact]
    public async Task Null_contributor_result_fails_closed()
    {
        StaffRetentionPrerequisiteEvaluation result =
            await new StaffRetentionPrerequisiteEvaluator(
                    [new StubPrerequisite("workspace-access")])
                .EvaluateAsync(Request(), CancellationToken.None);

        Assert.Equal(
            StaffRetentionPrerequisiteEvaluation.Unavailable,
            result);
    }

    private static StaffRetentionAnonymisationPrerequisiteRequest
        Request() =>
        new(
            StaffRetentionAnonymisationPrerequisiteContract
                .CurrentVersion,
            Guid.NewGuid(),
            "11111111-1111-1111-1111-111111111111",
            Guid.NewGuid(),
            SelectedStaffVersion: 4);

    private sealed class StubPrerequisite(string contributorKey)
        : IStaffRetentionAnonymisationPrerequisite
    {
        public string ContributorKey { get; } = contributorKey;

        public Task<StaffRetentionAnonymisationPrerequisiteResult>
            ExecuteAsync(
                StaffRetentionAnonymisationPrerequisiteRequest request,
                CancellationToken cancellationToken) =>
            Task.FromResult<StaffRetentionAnonymisationPrerequisiteResult>(
                null!);
    }
}
