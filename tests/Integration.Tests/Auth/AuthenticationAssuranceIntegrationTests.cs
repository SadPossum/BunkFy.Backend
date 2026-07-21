namespace Integration.Tests;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Security;
using Gma.Modules.Auth.Contracts;
using Integration.Tests.Support;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class AuthenticationAssuranceIntegrationTests
{
    private const string TenantId = "a7000000-0000-0000-0000-000000000001";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Stale_governance_token_can_step_up_and_retry_successfully()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_authentication_assurance_tests")
            .Build();
        await postgreSql.StartAsync();

        await using AuthTestApplication api = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            AuthTestContainers.GetNatsConnectionString(nats));
        await api.MigratePropertiesAuthorizationDatabaseAsync().ConfigureAwait(false);
        using HttpClient client = api.CreateClient();

        AuthTokensResponse registered = await AuthApiClient.RegisterAsync(
            client,
            TenantId,
            "owner@assurance.test").ConfigureAwait(false);
        Guid memberId = GetSubjectId(registered.AccessToken);
        await api.SeedOrganizationMembershipAsync(TenantId, memberId).ConfigureAwait(false);

        string staleAccessToken = CreateAccessTokenWithAuthenticationTime(
            registered.AccessToken,
            DateTimeOffset.UtcNow.AddMinutes(-11));
        using (HttpResponseMessage stale = await CreateInvitationAsync(
                   client,
                   staleAccessToken,
                   "stale@assurance.test").ConfigureAwait(false))
        {
            string body = await stale.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
            Assert.Contains("Security.InsufficientAuthentication", body, StringComparison.Ordinal);
            string challenge = stale.Headers.WwwAuthenticate.ToString();
            Assert.Contains("insufficient_user_authentication", challenge, StringComparison.Ordinal);
            Assert.Contains("max_age=\"600\"", challenge, StringComparison.Ordinal);
        }

        using HttpResponseMessage stepUp = await AuthApiClient.PostJsonAsync(
            client,
            TenantId,
            "/api/auth/step-up/password",
            new PasswordStepUpRequest(AuthApiClient.Password, registered.RefreshToken),
            staleAccessToken).ConfigureAwait(false);
        stepUp.EnsureSuccessStatusCode();
        AuthTokensResponse? fresh = await stepUp.Content.ReadFromJsonAsync<AuthTokensResponse>().ConfigureAwait(false);
        Assert.NotNull(fresh);

        using HttpResponseMessage accepted = await CreateInvitationAsync(
            client,
            fresh.AccessToken,
            "fresh@assurance.test").ConfigureAwait(false);
        string acceptedBody = await accepted.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(
            accepted.IsSuccessStatusCode,
            $"Expected governance retry to succeed but received {(int)accepted.StatusCode}. Body: {acceptedBody}");
    }

    private static Task<HttpResponseMessage> CreateInvitationAsync(
        HttpClient client,
        string accessToken,
        string recipientEmail) =>
        AuthApiClient.PostJsonAsync(
            client,
            TenantId,
            $"/api/organizations/{TenantId}/invitations",
            new { recipientEmail, lifetimeHours = 24 },
            accessToken);

    private static string CreateAccessTokenWithAuthenticationTime(
        string accessToken,
        DateTimeOffset authenticatedAtUtc)
    {
        JwtSecurityToken source = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string[] generatedClaims =
        [
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Nbf,
            JwtRegisteredClaimNames.Exp,
            JwtRegisteredClaimNames.Iss,
            JwtRegisteredClaimNames.Aud,
            ApplicationClaimNames.AuthenticationTime
        ];
        List<Claim> claims = source.Claims
            .Where(claim => !generatedClaims.Contains(claim.Type, StringComparer.Ordinal))
            .ToList();
        claims.Add(new Claim(
            ApplicationClaimNames.AuthenticationTime,
            authenticatedAtUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthTestApplication.JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            issuer: "BunkFy",
            audience: "BunkFy",
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: source.ValidTo,
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Guid GetSubjectId(string accessToken)
    {
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string? subjectId = token.Claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, ClaimTypes.NameIdentifier, StringComparison.Ordinal) ||
            string.Equals(claim.Type, "nameid", StringComparison.Ordinal) ||
            string.Equals(claim.Type, ApplicationClaimNames.Subject, StringComparison.Ordinal))?.Value;

        Assert.True(Guid.TryParse(subjectId, out Guid parsedSubjectId));
        return parsedSubjectId;
    }
}
