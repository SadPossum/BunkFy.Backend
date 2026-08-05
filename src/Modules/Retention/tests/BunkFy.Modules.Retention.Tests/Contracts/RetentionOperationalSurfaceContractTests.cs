namespace BunkFy.Modules.Retention.Tests.Contracts;

using BunkFy.Modules.Retention.Admin.Contracts;
using BunkFy.Modules.Retention.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionOperationalSurfaceContractTests
{
    [Fact]
    public void Health_and_retry_contracts_expose_bounded_operational_coordinates()
    {
        Assert.Equal(
            typeof(bool),
            typeof(RetentionScheduleHealthListResponse)
                .GetProperty(nameof(RetentionScheduleHealthListResponse.HasMore))!
                .PropertyType);
        Assert.Equal(
            typeof(RetentionScheduleHealthSummaryDto),
            typeof(RetentionScheduleHealthListResponse)
                .GetProperty(nameof(RetentionScheduleHealthListResponse.Summary))!
                .PropertyType);
        Assert.Equal(
            typeof(Guid?),
            typeof(RetentionScheduleHealthDto)
                .GetProperty(nameof(RetentionScheduleHealthDto.LastRunId))!
                .PropertyType);
        Assert.Equal(
            typeof(Guid),
            typeof(RetentionRunRetryReceiptDto)
                .GetProperty(nameof(RetentionRunRetryReceiptDto.RunId))!
                .PropertyType);
    }
}
