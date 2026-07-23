namespace BunkFy.Modules.DataRights.Domain.Models;

public enum DataRightsRequesterRelation
{
    Unknown = 0,
    DataSubject = 1,
    AuthorizedRepresentative = 2,
    ControllerInitiated = 3,
    TenantOwner = 4
}
