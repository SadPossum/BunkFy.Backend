namespace BunkFy.Host.ServiceDefaults.Security;

public sealed class BunkFyDataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public bool RequirePersistentKeys { get; set; }
    public string? KeyRingPath { get; set; }
    public string ApplicationName { get; set; } = "bunkfy";
}
