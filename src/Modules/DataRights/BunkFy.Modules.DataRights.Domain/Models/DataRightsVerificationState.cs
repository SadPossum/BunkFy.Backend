namespace BunkFy.Modules.DataRights.Domain.Models;

public enum DataRightsVerificationState
{
    Unknown = 0,
    Pending = 1,
    Verified = 2,
    Failed = 3,
    NotRequired = 4
}
