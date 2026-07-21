namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListOpenWorkspaceStaffAccessProcessesQueryHandler(
    IWorkspaceStaffAccessProcessRepository processes)
    : IQueryHandler<ListOpenWorkspaceStaffAccessProcessesQuery, WorkspaceStaffAccessProcessListResponse>
{
    public async Task<Result<WorkspaceStaffAccessProcessListResponse>> HandleAsync(
        ListOpenWorkspaceStaffAccessProcessesQuery query,
        CancellationToken cancellationToken) => Result.Success(
        await processes.ListOpenAsync(
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
}
