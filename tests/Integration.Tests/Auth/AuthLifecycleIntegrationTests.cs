namespace Integration.Tests;

using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Containers;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Errors;
using Integration.Tests.Support;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class AuthLifecycleIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Register_login_refresh_and_sign_out_runs_against_sql_server_and_postgre_sql()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();

        await RunAuthLifecycleAsync(
            "SqlServer",
            AuthTestContainers.GetNatsConnectionString(nats),
            async () =>
            {
                MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await sqlServer.StartAsync();
                return new ProviderLease(
                    sqlServer,
                    AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_auth_tests"));
            });

        await RunAuthLifecycleAsync(
            "PostgreSql",
            AuthTestContainers.GetNatsConnectionString(nats),
            async () =>
            {
                PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("gma_auth_tests")
                    .Build();
                await postgreSql.StartAsync();
                return new ProviderLease(postgreSql, postgreSql.GetConnectionString());
            });
    }

    private static async Task RunAuthLifecycleAsync(
        string provider,
        string natsConnectionString,
        Func<Task<ProviderLease>> createProvider)
    {
        await using ProviderLease providerLease = await createProvider().ConfigureAwait(false);
        await using AuthTestApplication application = new(provider, providerLease.ConnectionString, natsConnectionString);

        await application.MigrateDatabaseAsync().ConfigureAwait(false);
        using HttpClient client = application.CreateClient();

        string username = $"{provider.ToLowerInvariant()}@example.com";
        using HttpResponseMessage invalidUsernameType = await AuthApiClient.PostJsonAsync(
            client,
            "tenant-auth",
            "/api/auth/register",
            new
            {
                username = $"{provider.ToLowerInvariant()}-invalid-type@example.com",
                usernameType = 999,
                password = AuthApiClient.Password
            }).ConfigureAwait(false);
        string invalidUsernameTypeBody = await invalidUsernameType.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUsernameType.StatusCode);
        Assert.Contains(AuthApplicationErrors.UsernameTypeInvalid.Code, invalidUsernameTypeBody, StringComparison.Ordinal);

        AuthTokensResponse registered = await AuthApiClient.RegisterAsync(client, "tenant-auth", username).ConfigureAwait(false);
        using HttpResponseMessage duplicateUsername = await AuthApiClient.PostJsonAsync(
            client,
            "tenant-auth",
            "/api/auth/register",
            new RegisterMemberRequest(username, UsernameType.Email, AuthApiClient.Password)).ConfigureAwait(false);
        string duplicateUsernameBody = await duplicateUsername.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Conflict, duplicateUsername.StatusCode);
        Assert.Contains(AuthDomainErrors.UsernameAlreadyExists.Code, duplicateUsernameBody, StringComparison.Ordinal);

        AuthTokensResponse loggedIn = await AuthApiClient.LoginAsync(client, "tenant-auth", username).ConfigureAwait(false);
        AuthTokensResponse refreshed = await AuthApiClient.RefreshAsync(client, "tenant-auth", loggedIn).ConfigureAwait(false);

        using HttpResponseMessage signOut = await AuthApiClient.PostJsonAsync(
            client,
            "tenant-auth",
            "/api/auth/sign-out",
            new SignOutRequest(refreshed.RefreshToken),
            refreshed.AccessToken).ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(registered.AccessToken));

        await VerifyBrowserSessionAsync(client, username).ConfigureAwait(false);
    }

    private static async Task VerifyBrowserSessionAsync(HttpClient client, string username)
    {
        using HttpResponseMessage login = await AuthApiClient.PostJsonAsync(
            client,
            "tenant-auth",
            "/api/auth/browser/login",
            new LoginMemberRequest(username, AuthApiClient.Password)).ConfigureAwait(false);

        login.EnsureSuccessStatusCode();
        BrowserAuthResponse? loggedIn = await login.Content.ReadFromJsonAsync<BrowserAuthResponse>().ConfigureAwait(false);
        Assert.NotNull(loggedIn);
        Assert.False(string.IsNullOrWhiteSpace(loggedIn.AccessToken));
        string loginBody = await login.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.DoesNotContain("refreshToken", loginBody, StringComparison.OrdinalIgnoreCase);

        string[] setCookieHeaders = login.Headers.GetValues("Set-Cookie").ToArray();
        AssertBrowserCookies(setCookieHeaders);
        string loginRefreshCookie = GetRefreshCookieValue(setCookieHeaders);

        using HttpResponseMessage refresh = await AuthApiClient.PostAsync(
            client,
            "tenant-auth",
            "/api/auth/browser/refresh").ConfigureAwait(false);
        refresh.EnsureSuccessStatusCode();
        BrowserAuthResponse? refreshed = await refresh.Content.ReadFromJsonAsync<BrowserAuthResponse>().ConfigureAwait(false);
        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        string[] refreshedCookieHeaders = refresh.Headers.GetValues("Set-Cookie").ToArray();
        AssertBrowserCookies(refreshedCookieHeaders);
        Assert.False(
            string.Equals(loginRefreshCookie, GetRefreshCookieValue(refreshedCookieHeaders), StringComparison.Ordinal),
            "Browser refresh cookie must rotate without exposing its value.");

        using HttpResponseMessage signOut = await AuthApiClient.PostAsync(
            client,
            "tenant-auth",
            "/api/auth/browser/sign-out",
            refreshed.AccessToken).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        using HttpResponseMessage refreshAfterSignOut = await AuthApiClient.PostAsync(
            client,
            "tenant-auth",
            "/api/auth/browser/refresh").ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterSignOut.StatusCode);
    }

    private static void AssertBrowserCookies(string[] setCookieHeaders)
    {
        Assert.Equal(2, setCookieHeaders.Length);
        Assert.All(setCookieHeaders, cookie =>
        {
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("path=/api/auth/browser", cookie, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string GetRefreshCookieValue(string[] setCookieHeaders)
    {
        string header = Assert.Single(
            setCookieHeaders,
            cookie => cookie.StartsWith("gma.auth.refresh=", StringComparison.Ordinal));
        int separator = header.IndexOf(';', StringComparison.Ordinal);
        return separator < 0 ? header : header[..separator];
    }
}
