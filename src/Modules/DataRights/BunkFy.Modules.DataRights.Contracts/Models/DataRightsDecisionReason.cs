namespace BunkFy.Modules.DataRights.Contracts;

public enum DataRightsDecisionReason
{
    Unknown = 0,
    RequestValidated = 1,
    IdentityOrAuthorityNotEstablished = 2,
    RequestInvalid = 3,
    LegalObligation = 4,
    RightsOfOthers = 5,
    UnsupportedOperation = 6
}
