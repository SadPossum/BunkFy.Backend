namespace BunkFy.Adapters.SmtpEmail;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";

    public bool Enabled { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public SmtpSecurityMode SecurityMode { get; set; } = SmtpSecurityMode.StartTls;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? DefaultSenderAddress { get; set; }
    public string? DefaultSenderName { get; set; }
    public bool AllowSenderOverride { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public bool AllowUnauthenticatedInProduction { get; set; }
    public bool AllowInsecureTransportInProduction { get; set; }
}
