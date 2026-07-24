namespace BunkFy.Modules.DataRights.Application.Queries;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record DiscoverDataRightsSubjectsQuery(
    Guid PropertyId,
    Guid CaseId,
    DataRightsSubjectLookup Lookup) : IQuery<DataRightsSubjectDiscoveryResponse>;
