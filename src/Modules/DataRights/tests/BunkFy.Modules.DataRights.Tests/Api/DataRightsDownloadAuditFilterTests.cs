namespace BunkFy.Modules.DataRights.Tests.Api;

using System.Security.Claims;
using BunkFy.Modules.DataRights.Api;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Framework.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsDownloadAuditFilterTests
{
    [Fact]
    public async Task Pre_handler_forbidden_result_records_bounded_denial()
    {
        RecordingAuditSink audit = new();
        DefaultHttpContext context = Context(audit);
        DataRightsDownloadAuditFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(
                Results.Problem(statusCode: StatusCodes.Status403Forbidden)));

        Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        DataRightsExportAuditFact fact = Assert.Single(audit.Facts);
        Assert.Equal(DataRightsExportAuditAction.Download, fact.Action);
        Assert.Equal("denied-http-403", fact.OutcomeCode);
        Assert.Equal("user:user-1", fact.ActorId);
        Assert.Equal((Guid)context.Request.RouteValues["artifactId"]!, fact.ArtifactId);
    }

    [Fact]
    public async Task Handler_owned_result_is_not_audited_twice()
    {
        RecordingAuditSink audit = new();
        DefaultHttpContext context = Context(audit);
        DataRightsDownloadAuditFilter.MarkHandled(context);
        DataRightsDownloadAuditFilter filter = new();

        _ = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(
                Results.Problem(statusCode: StatusCodes.Status403Forbidden)));

        Assert.Empty(audit.Facts);
    }

    private static DefaultHttpContext Context(RecordingAuditSink audit)
    {
        ServiceCollection services = new();
        services.AddGmaAccessControlAspNetCore();
        services.AddSingleton<IScopeContext>(new ScopeContext());
        services.AddSingleton<IDataRightsExportAuditSink>(audit);
        services.AddSingleton<ISystemClock>(new Clock());
        DefaultHttpContext context = new()
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(GmaClaimNames.Subject, "user-1")],
                authenticationType: "test"))
        };
        context.Request.RouteValues["caseId"] = Guid.NewGuid();
        context.Request.RouteValues["artifactId"] = Guid.NewGuid();
        return context;
    }

    private sealed class RecordingAuditSink : IDataRightsExportAuditSink
    {
        public List<DataRightsExportAuditFact> Facts { get; } = [];

        public Task RecordAsync(
            DataRightsExportAuditFact fact,
            CancellationToken cancellationToken)
        {
            this.Facts.Add(fact);
            return Task.CompletedTask;
        }
    }

    private sealed class ScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class Clock : ISystemClock
    {
        public DateTimeOffset UtcNow =>
            new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    }
}
