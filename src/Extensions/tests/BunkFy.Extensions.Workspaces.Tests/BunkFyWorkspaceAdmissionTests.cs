namespace BunkFy.Extensions.Workspaces.Tests;

using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class BunkFyWorkspaceAdmissionTests
{
    [Fact]
    public void Registration_composes_explicit_policy_through_organizations_contracts()
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
            descriptor.ServiceType == typeof(IOrganizationCreationAdmissionPolicy) &&
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
        RecordingAdmissionReader admissions = new(
            new AuthMemberAdmission("verified@example.test"));
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            admissions,
            BunkFyWorkspaceCreationMode.Disabled,
            requireVerifiedEmail: true);

        OrganizationCreationAdmissionDecision decision = await policy.EvaluateAsync(
            Request(Guid.NewGuid().ToString("D")),
            CancellationToken.None);

        Assert.Equal(OrganizationCreationAdmissionDecision.Denied, decision);
        Assert.Equal(0, admissions.LookupCount);
    }

    [Fact]
    public async Task Local_self_service_can_skip_email_verification_but_still_requires_an_active_member()
    {
        RecordingAdmissionReader admissions = new(new AuthMemberAdmission(null));
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            admissions,
            BunkFyWorkspaceCreationMode.SelfService,
            requireVerifiedEmail: false);

        OrganizationCreationAdmissionDecision decision = await policy.EvaluateAsync(
            Request(Guid.NewGuid().ToString("D")),
            CancellationToken.None);

        Assert.Equal(OrganizationCreationAdmissionDecision.Allowed, decision);
        Assert.Equal(1, admissions.LookupCount);
    }

    [Fact]
    public async Task Inactive_member_cannot_create_a_workspace_when_email_verification_is_optional()
    {
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            new RecordingAdmissionReader(null),
            BunkFyWorkspaceCreationMode.SelfService,
            requireVerifiedEmail: false);

        OrganizationCreationAdmissionDecision decision = await policy.EvaluateAsync(
            Request(Guid.NewGuid().ToString("D")),
            CancellationToken.None);

        Assert.Equal(
            OrganizationCreationAdmissionDecision.SubjectVerificationRequired,
            decision);
    }

    [Theory]
    [InlineData("not-a-member-id", null)]
    [InlineData("00000000-0000-0000-0000-000000000001", null)]
    [InlineData("00000000-0000-0000-0000-000000000001", "   ")]
    public async Task Verified_email_policy_denies_unverified_subjects(string subjectId, string? verifiedEmail)
    {
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            new RecordingAdmissionReader(new AuthMemberAdmission(verifiedEmail)),
            BunkFyWorkspaceCreationMode.SelfService,
            requireVerifiedEmail: true);

        OrganizationCreationAdmissionDecision decision = await policy.EvaluateAsync(
            Request(subjectId),
            CancellationToken.None);

        Assert.Equal(
            OrganizationCreationAdmissionDecision.SubjectVerificationRequired,
            decision);
    }

    [Fact]
    public async Task Verified_member_can_create_a_self_service_workspace()
    {
        Guid memberId = Guid.NewGuid();
        RecordingAdmissionReader admissions = new(
            new AuthMemberAdmission("verified@example.test"));
        BunkFyWorkspaceAdmissionPolicy policy = CreatePolicy(
            admissions,
            BunkFyWorkspaceCreationMode.SelfService,
            requireVerifiedEmail: true);

        OrganizationCreationAdmissionDecision decision = await policy.EvaluateAsync(
            Request(memberId.ToString("D")),
            CancellationToken.None);

        Assert.Equal(OrganizationCreationAdmissionDecision.Allowed, decision);
        Assert.Equal("bunkfy-auth", admissions.LastScopeId);
        Assert.Equal(memberId, admissions.LastMemberId);
    }

    private static BunkFyWorkspaceAdmissionPolicy CreatePolicy(
        IAuthMemberAdmissionReader admissions,
        BunkFyWorkspaceCreationMode mode,
        bool requireVerifiedEmail) => new(
            admissions,
            Options.Create(new BunkFyWorkspacesOptions { GlobalAuthScopeId = "bunkfy-auth" }),
            Options.Create(new BunkFyWorkspaceAdmissionOptions
            {
                AccountRegistration = BunkFyAccountRegistrationMode.Public,
                WorkspaceCreation = mode,
                RequireVerifiedEmailForWorkspaceCreation = requireVerifiedEmail
            }));

    private static OrganizationCreationAdmissionRequest Request(string subjectId) =>
        new(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Harbor House",
            "harbor-house",
            subjectId,
            $"user:{subjectId}");

    private sealed class RecordingAdmissionReader(AuthMemberAdmission? admission)
        : IAuthMemberAdmissionReader
    {
        public int LookupCount { get; private set; }

        public string? LastScopeId { get; private set; }

        public Guid LastMemberId { get; private set; }

        public ValueTask<AuthMemberAdmission?> FindActiveAsync(
            string scopeId,
            Guid memberId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.LookupCount++;
            this.LastScopeId = scopeId;
            this.LastMemberId = memberId;
            return ValueTask.FromResult(admission);
        }
    }
}
