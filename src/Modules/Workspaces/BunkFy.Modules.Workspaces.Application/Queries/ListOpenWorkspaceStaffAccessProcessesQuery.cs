namespace BunkFy.Modules.Workspaces.Application.Queries;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record ListOpenWorkspaceStaffAccessProcessesQuery(
    int Page,
    int PageSize) : IQuery<WorkspaceStaffAccessProcessListResponse>;
