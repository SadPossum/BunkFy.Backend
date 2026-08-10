namespace BunkFy.Adapters.Tests;

using System.Net.Sockets;
using BunkFy.Adapters.SmtpEmail;
using Gma.Framework.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class SmtpEmailSenderTests
{
    private static readonly EmailSendRequest Request = new(
        "recipient@example.test",
        "Welcome",
        "Hello",
        htmlBody: null,
        "notification:0123456789abcdef",
        "requested@example.test",
        "Requested sender");

    [Fact]
    public void Disabled_adapter_requires_no_transport_configuration_and_registers_no_sender()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddBunkFySmtpEmailSender(configuration, isProduction: true);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IEmailSender>());
    }

    [Fact]
    public void Enabled_adapter_registers_the_generic_sender_contract()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddBunkFySmtpEmailSender(configuration, isProduction: false);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<SmtpEmailSender>(provider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void Enabled_adapter_rejects_incomplete_configuration_before_host_build()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Smtp:Enabled"] = "true",
                ["Email:Smtp:Host"] = "https://smtp.example.test/path",
                ["Email:Smtp:DefaultSenderAddress"] = "invalid"
            })
            .Build();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddBunkFySmtpEmailSender(configuration, isProduction: false));

        Assert.Contains(exception.Failures, failure => failure.Contains("Email:Smtp:Host", StringComparison.Ordinal));
        Assert.Contains(exception.Failures, failure => failure.Contains("DefaultSenderAddress", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_plaintext_or_unauthenticated_submission_by_default()
    {
        IConfiguration configuration = CreateConfiguration(
            securityMode: SmtpSecurityMode.None,
            username: null,
            password: null);
        ServiceCollection services = new();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddBunkFySmtpEmailSender(configuration, isProduction: true));

        Assert.Contains(exception.Failures, failure => failure.Contains("requires StartTls", StringComparison.Ordinal));
        Assert.Contains(exception.Failures, failure => failure.Contains("requires authenticated", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_exceptions_must_be_explicit_and_keep_the_adapter_composable()
    {
        IConfiguration configuration = CreateConfiguration(
            securityMode: SmtpSecurityMode.None,
            username: null,
            password: null,
            allowUnauthenticatedInProduction: true,
            allowInsecureTransportInProduction: true);
        ServiceCollection services = new();

        services.AddBunkFySmtpEmailSender(configuration, isProduction: true);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public async Task Sender_uses_configured_identity_and_stable_non_reversible_message_id()
    {
        RecordingTransport transport = new();
        SmtpEmailSender sender = CreateSender(transport);

        EmailSendResult first = await sender.SendAsync(Request);
        EmailSendResult second = await sender.SendAsync(Request);

        Assert.Equal(EmailSendOutcome.Delivered, first.Outcome);
        Assert.Equal(first.ProviderMessageId, second.ProviderMessageId);
        Assert.DoesNotContain(Request.IdempotencyKey, first.ProviderMessageId, StringComparison.Ordinal);
        Assert.Matches("^bunkfy-[0-9a-f]{64}@example\\.test$", first.ProviderMessageId!);
        Assert.Collection(
            transport.Envelopes,
            envelope => AssertConfiguredEnvelope(envelope, first.ProviderMessageId!),
            envelope => AssertConfiguredEnvelope(envelope, first.ProviderMessageId!));
    }

    [Fact]
    public async Task Sender_override_is_applied_only_when_explicitly_enabled()
    {
        RecordingTransport transport = new();
        SmtpEmailSender sender = CreateSender(
            transport,
            new SmtpEmailOptions
            {
                Enabled = true,
                Host = "smtp.example.test",
                DefaultSenderAddress = "default@example.test",
                DefaultSenderName = "Default sender",
                AllowSenderOverride = true
            });

        _ = await sender.SendAsync(Request);

        SmtpEmailEnvelope envelope = Assert.Single(transport.Envelopes);
        Assert.Equal("requested@example.test", envelope.SenderAddress);
        Assert.Equal("Requested sender", envelope.SenderName);
    }

    [Theory]
    [InlineData(421, EmailSendOutcome.Retry, "smtp-transient")]
    [InlineData(450, EmailSendOutcome.Retry, "smtp-transient")]
    [InlineData(550, EmailSendOutcome.Rejected, "smtp-rejected")]
    [InlineData(0, EmailSendOutcome.Retry, "smtp-command")]
    public void Smtp_statuses_map_to_bounded_provider_neutral_outcomes(
        int status,
        EmailSendOutcome outcome,
        string code)
    {
        EmailSendResult result = SmtpEmailSender.ClassifyStatusCode((SmtpStatusCode)status);

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(code, result.Code);
    }

    [Fact]
    public async Task Transport_failures_return_a_bounded_retry_without_exception_content()
    {
        RecordingTransport transport = new(new IOException("recipient@example.test private response"));
        SmtpEmailSender sender = CreateSender(transport);

        EmailSendResult result = await sender.SendAsync(Request);

        Assert.Equal(EmailSendOutcome.Retry, result.Outcome);
        Assert.Equal("smtp-transport", result.Code);
        Assert.DoesNotContain("recipient", result.Code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Socket_failures_return_a_bounded_transport_retry()
    {
        RecordingTransport transport = new(new SocketException());
        SmtpEmailSender sender = CreateSender(transport);

        EmailSendResult result = await sender.SendAsync(Request);

        Assert.Equal(EmailSendOutcome.Retry, result.Outcome);
        Assert.Equal("smtp-transport", result.Code);
    }

    [Fact]
    public async Task Tls_handshake_failures_return_a_bounded_rejection()
    {
        RecordingTransport transport = new(new SslHandshakeException("private detail"));
        SmtpEmailSender sender = CreateSender(transport);

        EmailSendResult result = await sender.SendAsync(Request);

        Assert.Equal(EmailSendOutcome.Rejected, result.Outcome);
        Assert.Equal("smtp-tls", result.Code);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted_into_a_delivery_retry()
    {
        using CancellationTokenSource source = new();
        source.Cancel();
        RecordingTransport transport = new(new OperationCanceledException(source.Token));
        SmtpEmailSender sender = CreateSender(transport);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await sender.SendAsync(Request, source.Token));
    }

    private static void AssertConfiguredEnvelope(SmtpEmailEnvelope envelope, string messageId)
    {
        Assert.Equal("default@example.test", envelope.SenderAddress);
        Assert.Equal("BunkFy", envelope.SenderName);
        Assert.Equal(Request.RecipientAddress, envelope.RecipientAddress);
        Assert.Equal(Request.Subject, envelope.Subject);
        Assert.Equal(messageId, envelope.MessageId);
    }

    private static SmtpEmailSender CreateSender(
        RecordingTransport transport,
        SmtpEmailOptions? options = null) =>
        new(
            transport,
            Options.Create(options ?? new SmtpEmailOptions
            {
                Enabled = true,
                Host = "smtp.example.test",
                DefaultSenderAddress = "default@example.test",
                DefaultSenderName = "BunkFy"
            }));

    private static IConfiguration CreateConfiguration(
        SmtpSecurityMode securityMode = SmtpSecurityMode.StartTls,
        string? username = "mailer",
        string? password = "private",
        bool allowUnauthenticatedInProduction = false,
        bool allowInsecureTransportInProduction = false) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Smtp:Enabled"] = "true",
                ["Email:Smtp:Host"] = "smtp.example.test",
                ["Email:Smtp:Port"] = "587",
                ["Email:Smtp:SecurityMode"] = securityMode.ToString(),
                ["Email:Smtp:Username"] = username,
                ["Email:Smtp:Password"] = password,
                ["Email:Smtp:DefaultSenderAddress"] = "default@example.test",
                ["Email:Smtp:DefaultSenderName"] = "BunkFy",
                ["Email:Smtp:TimeoutSeconds"] = "15",
                ["Email:Smtp:AllowUnauthenticatedInProduction"] = allowUnauthenticatedInProduction.ToString(),
                ["Email:Smtp:AllowInsecureTransportInProduction"] = allowInsecureTransportInProduction.ToString()
            })
            .Build();

    private sealed class RecordingTransport(Exception? exception = null) : ISmtpEmailTransport
    {
        public List<SmtpEmailEnvelope> Envelopes { get; } = [];

        public ValueTask SendAsync(
            SmtpEmailEnvelope envelope,
            SmtpEmailOptions options,
            CancellationToken cancellationToken)
        {
            this.Envelopes.Add(envelope);
            return exception is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(exception);
        }
    }
}
