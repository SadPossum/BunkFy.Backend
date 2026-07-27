namespace BunkFy.Modules.DataRights.Application.Queries;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record DiscoverDataRightsSubjectsQuery(
    DataRightsCaseScope Scope,
    Guid CaseId,
    DataRightsSubjectLookup Lookup,
    string? OwnerKey = null) : IQuery<DataRightsSubjectDiscoveryResponse>;
