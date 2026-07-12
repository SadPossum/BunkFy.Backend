namespace BunkFy.Modules.Ingestion.Domain.Credentials;

public enum AdapterIngressCredentialState
{
    Unknown = 0,
    Active = 1,
    Revoked = 2,
    Expired = 3
}
