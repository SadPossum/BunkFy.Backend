namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record PrepareDataRightsExportDownloadCommand(
    DataRightsCaseScope Scope,
    Guid CaseId,
    Guid ArtifactId,
    string ActorId) : ICommand<DataRightsExportDownload>;
