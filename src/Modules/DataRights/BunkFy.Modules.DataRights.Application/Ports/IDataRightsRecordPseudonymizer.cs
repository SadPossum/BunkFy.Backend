namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;

public interface IDataRightsRecordPseudonymizer
{
    Result<DataRightsRecordPseudonym> CreateActive(
        string tenantId,
        string ownerKey,
        string recordType,
        Guid recordId);

    Result<DataRightsRecordPseudonym> Create(
        int keyVersion,
        string tenantId,
        string ownerKey,
        string recordType,
        Guid recordId);
}
