namespace BunkFy.Modules.Retention.Tests.Application;

using BunkFy.Modules.Retention.Application;
using BunkFy.Modules.Retention.Application.Commands;
using BunkFy.Modules.Retention.Application.Tasks;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class RetentionCommandContractTests
{
    [Fact]
    public void Execution_commands_participate_in_the_module_unit_of_work()
    {
        Assert.Contains(
            typeof(ITransactionalCommand<RetentionExecutionStart>),
            typeof(BeginRetentionExecutionCommand).GetInterfaces());
        Assert.Contains(
            typeof(ITransactionalCommand<Unit>),
            typeof(CompleteRetentionExecutionCommand).GetInterfaces());
    }

    [Fact]
    public void Task_handler_timeout_covers_the_maximum_contributor_deadline()
    {
        ServiceCollection services = new();

        services.AddRetentionTaskHandlers();

        TaskHandlerRegistration registration = Assert.IsType<TaskHandlerRegistration>(
            Assert.Single(
                services,
                descriptor => descriptor.ServiceType ==
                    typeof(TaskHandlerRegistration)).ImplementationInstance);
        TimeSpan timeout = Assert.IsType<TimeSpan>(registration.HandlerTimeout);
        Assert.Equal(
            RetentionTaskExecutionPolicy.HandlerTimeout,
            timeout);
        Assert.True(
            timeout >
            RetentionExecutionContract.MaximumContributorExecutionTimeout);
        Assert.Equal(
            RetentionTaskExecutionPolicy.CompletionGrace,
            timeout -
                RetentionExecutionContract.MaximumContributorExecutionTimeout);
    }

    [Fact]
    public void Contributor_timeout_cannot_exceed_the_task_handler_budget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RetentionScheduleDescriptor(
                "owner",
                "data-class",
                RetentionTargetScopeKind.Tenant,
                executionPolicyVersion: 1,
                interval: TimeSpan.FromHours(1),
                executionTimeout:
                    RetentionExecutionContract.MaximumContributorExecutionTimeout +
                    TimeSpan.FromMilliseconds(1)));
    }
}
