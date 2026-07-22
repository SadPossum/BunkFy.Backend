namespace BunkFy.Host.ServiceDefaults.Tests.Observability;

using System.Diagnostics;
using BunkFy.Host.ServiceDefaults.Observability;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PrivacyPreservingActivityProcessorTests
{
    [Fact]
    public void Http_activity_keeps_operational_dimensions_and_removes_personal_data_canaries()
    {
        const string guestIdentifierCanary = "guest-sensitive-491";
        const string queryCanary = "guest491@example.test";
        const string addressCanary = "203.0.113.49";

        using Activity activity = new($"GET /guests/{guestIdentifierCanary}");
        activity.SetTag("http.request.method", "GET");
        activity.SetTag("http.route", "/guests/{guestId}");
        activity.SetTag("http.response.status_code", 200);
        activity.SetTag("url.full", $"https://example.test/guests/{guestIdentifierCanary}?email={queryCanary}");
        activity.SetTag("url.path", $"/guests/{guestIdentifierCanary}");
        activity.SetTag("url.query", $"email={queryCanary}");
        activity.SetTag("client.address", addressCanary);
        activity.SetTag("http.request.header.x-guest-name", "Private Guest");
        activity.SetTag("custom.guest.identifier", guestIdentifierCanary);
        activity.SetBaggage("guest.email", queryCanary);
        PrivacyPreservingActivityProcessor processor = new();

        processor.OnEnd(activity);

        Assert.Equal("GET /guests/{guestId}", activity.DisplayName);
        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
        Assert.Equal("/guests/{guestId}", activity.GetTagItem("http.route"));
        Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
        Assert.Null(activity.GetTagItem("url.full"));
        Assert.Null(activity.GetTagItem("url.path"));
        Assert.Null(activity.GetTagItem("url.query"));
        Assert.Null(activity.GetTagItem("client.address"));
        Assert.Null(activity.GetTagItem("http.request.header.x-guest-name"));
        Assert.Null(activity.GetTagItem("custom.guest.identifier"));
        Assert.Empty(activity.Baggage);
    }

    [Fact]
    public void Error_type_is_kept_only_when_it_is_a_bounded_type_token()
    {
        using Activity safeActivity = new("safe");
        safeActivity.SetTag("http.request.method", "POST");
        safeActivity.SetTag("error.type", "System.TimeoutException");
        using Activity unsafeActivity = new("unsafe");
        unsafeActivity.SetTag("http.request.method", "POST");
        unsafeActivity.SetTag("error.type", "Guest private.person@example.test timed out");
        PrivacyPreservingActivityProcessor processor = new();

        processor.OnEnd(safeActivity);
        processor.OnEnd(unsafeActivity);

        Assert.Equal("System.TimeoutException", safeActivity.GetTagItem("error.type"));
        Assert.Null(unsafeActivity.GetTagItem("error.type"));
    }
}
