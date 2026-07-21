namespace BunkFy.Modules.Ingestion.Api;

using Gma.Framework.Security;

public sealed class IngestionApiSecurityOptions
{
    public AuthenticationAssuranceRequirement? CredentialManagementAssurance { get; set; }
}
