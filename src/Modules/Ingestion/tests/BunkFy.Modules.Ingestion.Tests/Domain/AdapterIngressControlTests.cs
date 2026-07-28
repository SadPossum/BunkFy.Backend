namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Modules.Ingestion.Domain.Controls;
using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterIngressControlTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Tenant_control_tracks_actor_reason_timestamps_and_optimistic_version()
    {
        AdapterIngressTenantControl control = AdapterIngressTenantControl.CreateSuspended(
            " tenant-a ",
            "security.review",
            "user:owner",
            Now).Value;

        Assert.Equal("tenant-a", control.Id);
        Assert.Equal(control.Id, control.ScopeId);
        Assert.True(control.IsSuspended);
        Assert.Equal(1, control.Version);
        Assert.Equal(Now, control.SuspendedAtUtc);

        Result stale = control.Resume(0, "review.complete", "user:owner", Now.AddMinutes(1));
        Result resumed = control.Resume(1, "review.complete", "user:owner", Now.AddMinutes(1));

        Assert.Equal(IngestionDomainErrors.VersionConflict, stale.Error);
        Assert.True(resumed.IsSuccess);
        Assert.False(control.IsSuspended);
        Assert.Equal(2, control.Version);
        Assert.Equal(Now.AddMinutes(1), control.ResumedAtUtc);
    }

    [Fact]
    public void Global_control_rejects_unbounded_or_free_text_reason_codes()
    {
        Result<AdapterIngressGlobalControl> whitespace =
            AdapterIngressGlobalControl.CreateStopped(
                "free text reason",
                "admin:operator",
                Now);
        Result<AdapterIngressGlobalControl> overlong =
            AdapterIngressGlobalControl.CreateStopped(
                new string('a', AdapterIngressTenantControl.ReasonCodeMaxLength + 1),
                "admin:operator",
                Now);

        Assert.Equal(IngestionDomainErrors.AdapterIngressControlDecisionInvalid, whitespace.Error);
        Assert.Equal(IngestionDomainErrors.AdapterIngressControlDecisionInvalid, overlong.Error);
    }

    [Fact]
    public void Global_stop_cannot_be_reapplied_without_an_explicit_resume()
    {
        AdapterIngressGlobalControl control = AdapterIngressGlobalControl.CreateStopped(
            "incident.active",
            "admin:operator",
            Now).Value;

        Result duplicate = control.Stop(1, "incident.active", "admin:operator", Now.AddMinutes(1));
        Result resumed = control.Resume(1, "incident.resolved", "admin:operator", Now.AddMinutes(2));

        Assert.Equal(IngestionDomainErrors.AdapterIngressGlobalAlreadyStopped, duplicate.Error);
        Assert.True(resumed.IsSuccess);
        Assert.False(control.IsStopped);
        Assert.Equal(2, control.Version);
    }
}
