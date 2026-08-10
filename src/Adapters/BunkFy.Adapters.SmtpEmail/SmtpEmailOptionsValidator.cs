namespace BunkFy.Adapters.SmtpEmail;

using Gma.Framework.Email;
using Microsoft.Extensions.Options;

internal sealed class SmtpEmailOptionsValidator(bool isProduction)
    : IValidateOptions<SmtpEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpEmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        if (!IsValidHost(options.Host))
        {
            failures.Add("Email:Smtp:Host must be a valid DNS name or IP address without a scheme or path.");
        }

        if (options.Port is < 1 or > 65_535)
        {
            failures.Add("Email:Smtp:Port must be between 1 and 65535.");
        }

        if (!Enum.IsDefined(options.SecurityMode) || options.SecurityMode == SmtpSecurityMode.Unknown)
        {
            failures.Add("Email:Smtp:SecurityMode must be None, StartTls, or SslOnConnect.");
        }

        bool hasUsername = !string.IsNullOrWhiteSpace(options.Username);
        bool hasPassword = !string.IsNullOrWhiteSpace(options.Password);
        if (hasUsername != hasPassword)
        {
            failures.Add("Email:Smtp:Username and Email:Smtp:Password must either both be configured or both be absent.");
        }

        if (hasUsername &&
            (options.Username!.Length > 320 || options.Username.Any(char.IsControl)))
        {
            failures.Add("Email:Smtp:Username must be 320 characters or fewer and cannot contain control characters.");
        }

        if (hasPassword && options.Password!.Length > 4096)
        {
            failures.Add("Email:Smtp:Password must be 4096 characters or fewer.");
        }

        if (!EmailSendRequest.IsValidAddress(options.DefaultSenderAddress))
        {
            failures.Add("Email:Smtp:DefaultSenderAddress must be a valid email address.");
        }

        if (options.DefaultSenderName?.Length > 256 ||
            options.DefaultSenderName?.Any(char.IsControl) == true)
        {
            failures.Add("Email:Smtp:DefaultSenderName must be 256 characters or fewer and cannot contain control characters.");
        }

        if (options.TimeoutSeconds is < 1 or > 120)
        {
            failures.Add("Email:Smtp:TimeoutSeconds must be between 1 and 120.");
        }

        if (isProduction &&
            options.SecurityMode == SmtpSecurityMode.None &&
            !options.AllowInsecureTransportInProduction)
        {
            failures.Add("Production SMTP requires StartTls or SslOnConnect unless Email:Smtp:AllowInsecureTransportInProduction is explicitly enabled.");
        }

        if (isProduction && !hasUsername && !options.AllowUnauthenticatedInProduction)
        {
            failures.Add("Production SMTP requires authenticated submission unless Email:Smtp:AllowUnauthenticatedInProduction is explicitly enabled.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            host.Length > 253 ||
            !string.Equals(host, host.Trim(), StringComparison.Ordinal) ||
            host.Any(char.IsControl))
        {
            return false;
        }

        return Uri.CheckHostName(host) != UriHostNameType.Unknown;
    }
}
