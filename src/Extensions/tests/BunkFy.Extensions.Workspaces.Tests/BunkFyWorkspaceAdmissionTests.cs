namespace BunkFy.Extensions.Workspaces.Tests;

using Gma.Framework.Results;
using Gma.Modules.Auth.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class BunkFyWorkspaceAdmissionTests
{
    [Fact]
    public void Registration_binds_explicit_policy_and_replaces_the_organizations_admission_port()
    {
        ConfigurationManager configuration = new();
        configuration["Auth:SelfRegistration:PasswordEnabled"] = "true";
        configuration["Auth:SelfRegistration:ExternalEnabled"] = "false";
        configuration["Organizations:SelfServiceCreationEnabled"] = "true";
        configuration[$"{BunkFyWorkspaceAdmissionOptions.SectionName}:AccountRegistration"] = "Public";
        configuration[$"{BunkFyWorkspaceAdmissionOptions.SectionName}:WorkspaceCreation"] = "SelfService";
        configuration[$"{BunkFyWorkspaceAdmissionOptions.SectionName}:RequireVerifiedEmailForWorkspaceCreation"] = "true";
        ServiceCollection services = new();

        services.AddBunkFyWorkspaces(options => options.GlobalAuthScopeId = "bunkfy-auth");
        services.AddBunkFyWorkspaceAdmission(configuration, requireExplicitPolicy: true);

        using ServiceProvider provider = services.BuildServiceProvider();
        BunkFyWorkspaceAdmissionOptions options = provider
            .GetRequiredService<IOptions<BunkFyWorkspaceAdmissionOptions>>()
            .Value;
        Assert.Equal(BunkFyAccountRegistrationMode.Public, options.AccountRegistration);
        Assert.Equal(BunkFyWorkspaceCreationMode.SelfService, options.WorkspaceCreation);
        Assert.True(options.RequireVerifiedEmailForWorkspaceCreation);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.FullName ==
            "Gma.Modules.Organizations.Application.Ports.IOrganizationAdmissionPolicy" &&
            descriptor.ImplementationType == typeof(BunkFyWorkspaceAdmissionPolicy));
    }

    [Fact]
    public void Production_requires_explicit_account_and_workspace_modes()
    {
        BunkFyWorkspaceAdmissionOptionsValidator validator = new(
            requireExplicitPolicy: true,
            passwordRegistrationEnabled: true,
            externalRegistrationEnabled: true,
            selfServiceWorkspaceCreationEnabled: true);

        ValidateOptionsResult result = validator.Validate(null, new BunkFyWorkspaceAdmissionOptions());

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("AccountRegistration", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("WorkspaceCreation", StringComparison.Ordinal));
    }

    [Fact]
    public void Coherent_public_self_service_policy_is_valid()
    {
        BunkFyWorkspaceAdmissionOptionsValidator validator = new(
            requireExplicitPolicy: true,
            passwordRegistrationEnabled: true,
            externalRegistrationEnabled: false,
            selfServiceWorkspaceCreationEnabled: true);
        BunkFyWorkspaceAdmissionOptions options = new()
        {
            AccountRegistration = BunkFyAccountRegistrationMode.Public,
            WorkspaceCreation = BunkFyWorkspaceCreationMode.SelfService
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(BunkFyAccountRegistrationMode.Public, false, false)]
    [InlineData(BunkFyAccountRegistrationMode.Disabled, true, false)]
    [InlineData(BunkFyAccountRegistrationMode.Disabled, false, true)]
    public void Account_policy_must_match_auth_registration(
        BunkFyAccountRegistrationMode mode,
        bool passwordEnabled,
        bool externalEnabled)
    {
        BunkFyWorkspaceAdmissionOptionsValidator validator = new(
            requireExplicitPolicy: true,
            passwordEnabled,
            externalEnabled,
            selfServiceWorkspaceCreationEnabled: false);
        BunkFyWorkspaceAdmissionOptions options = new()
        {
            AccountRegistration = mode,
            WorkspaceCreation = BunkFyWorkspaceCreationMode.Disabled
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("account registration", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(BunkFyWorkspaceCreationMode.SelfService, false)]
    [InlineData(BunkFyWorkspaceCreationMode.Disabled, true)]
    public void Workspace_policy_must_match_organizations_configuration(
        BunkFyWorkspaceCreationMode mode,
        bool selfServiceEnabled)
    {
        BunkFyWorkspaceAdmissionOptionsValidator validator = new(
            requireExplicitPolicy: true,
            passwordRegistrationEnabled: false,
            externalRegistrationEnabled: false,
            selfServiceEnabled);
        BunkFyWorkspaceAdmissionOptions options = new()
        {
            AccountRegistration = BunkFyAccountRegistrationMode.Disabled,
            WorkspaceCreation = mode
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("workspace creation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Disabled_workspace_creation_is_denied_before_contact_lookup()
    {
        RecordingContactReader contacts = new("verified@example.test");
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            contacts,
            BunkFyWorkspaceCreationMode.Disabled,
            requireVerifiedEmail: true);

        Result result = await policy.CanCreateOrganizationAsync(Guid.NewGuid().ToString("D"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Organizations.SelfServiceCreationDisabled", result.Error.Code);
        Assert.Equal(0, contacts.LookupCount);
    }

    [Fact]
    public async Task Local_self_service_can_explicitly_skip_email_verification()
    {
        RecordingContactReader contacts = new(null);
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            contacts,
            BunkFyWorkspaceCreationMode.SelfService,
            requireVerifiedEmail: false);

        Result result = await policy.CanCreateOrganizationAsync("local-subject", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, contacts.LookupCount);
    }

    [Theory]
    [InlineData("not-a-member-id", null)]
    [InlineData("00000000-0000-0000-0000-000000000001", null)]
    [InlineData("00000000-0000-0000-0000-000000000001", "   ")]
    public async Task Verified_email_policy_denies_unverified_subjects(string subjectId, string? verifiedEmail)
    {
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            new RecordingContactReader(verifiedEmail),
            BunkFyWorkspaceCreationMode.SelfService,
            requireVerifiedEmail: true);

        Result result = await policy.CanCreateOrganizationAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Organizations.SubjectVerificationRequired", result.Error.Code);
    }

    [Fact]
    public async Task Verified_member_can_create_a_self_service_workspace()
    {
        Guid memberId = Guid.NewGuid();
        RecordingContactReader contacts = new("verified@example.test");
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            contacts,
            BunkFyWorkspaceCreationMode.SelfService,
            requireVerifiedEmail: true);

        Result result = await policy.CanCreateOrganizationAsync(memberId.ToString("D"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("bunkfy-auth", contacts.LastScopeId);
        Assert.Equal(memberId, contacts.LastMemberId);
    }

    private static BunkFyWorkspaceAdmissionPolicy CreatePolicy(
        IAuthMemberContactReader contacts,
        BunkFyWorkspaceCreationMode mode,
        bool requireVerifiedEmail) => new(
            contacts,
            Options.Create(new BunkFyWorkspacesOptions { GlobalAuthScopeId = "bunkfy-auth" }),
            Options.Create(new BunkFyWorkspaceAdmissionOptions
            {
                AccountRegistration = BunkFyAccountRegistrationMode.Public,
                WorkspaceCreation = mode,
                RequireVerifiedEmailForWorkspaceCreation = requireVerifiedEmail
            }));

    private sealed class RecordingContactReader(string? verifiedEmail) : IAuthMemberContactReader
    {
        public int LookupCount { get; private set; }

        public string? LastScopeId { get; private set; }

        public Guid LastMemberId { get; private set; }

        public ValueTask<string?> GetPreferredVerifiedEmailAsync(
            string scopeId,
            Guid memberId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.LookupCount++;
            this.LastScopeId = scopeId;
            this.LastMemberId = memberId;
            return ValueTask.FromResult(verifiedEmail);
        }
    }
}
