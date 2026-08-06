namespace BunkFy.Modules.Workspaces.Tests;

using System.Reflection;
using BunkFy.Modules.Workspaces.Application.Handlers;
using BunkFy.Modules.Workspaces.Application.Ports;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceCrossGraphMutationWriterTests
{
    [Theory]
    [InlineData(typeof(ScrubWorkspaceStaffRetentionCorrelationCommandHandler))]
    [InlineData(typeof(ApplyWorkspaceStaffCorrelationAnonymisationCommandHandler))]
    [InlineData(typeof(RestoreWorkspaceStaffCorrelationAnonymisationCommandHandler))]
    public void Subject_wide_writers_require_cross_graph_lock(
        Type writerType)
    {
        bool hasLock = writerType
            .GetConstructors(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType ==
                typeof(IWorkspaceCrossGraphMutationLock));

        Assert.True(
            hasLock,
            $"{writerType.Name} must acquire the Workspaces cross-graph lock.");
    }
}
