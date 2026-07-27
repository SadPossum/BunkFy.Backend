namespace BunkFy.Modules.Retention.Tests.Application;

using BunkFy.Modules.Retention.Application.Commands;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
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
}
