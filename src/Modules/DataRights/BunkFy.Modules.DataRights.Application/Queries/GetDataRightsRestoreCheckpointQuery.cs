namespace BunkFy.Modules.DataRights.Application.Queries;

using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.Cqrs;

internal sealed record GetDataRightsRestoreCheckpointQuery
    : IQuery<DataRightsRestoreCheckpointState>;
