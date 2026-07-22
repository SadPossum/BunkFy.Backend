namespace BunkFy.Modules.Workspaces.Application;

using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;

internal interface IWorkspaceAuthoritativeScope
{
    Task<TResult> RunAsync<TResult>(
        Guid organizationId,
        Func<IServiceProvider, Task<TResult>> operation);
}

internal sealed class WorkspaceAuthoritativeScope(IServiceScopeFactory scopeFactory)
    : IWorkspaceAuthoritativeScope
{
    public async Task<TResult> RunAsync<TResult>(
        Guid organizationId,
        Func<IServiceProvider, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using IServiceScope scope = scopeFactory.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IScopeContextAccessor>()
            .SetScope(organizationId.ToString("D"));

        return await operation(scope.ServiceProvider).ConfigureAwait(false);
    }
}
