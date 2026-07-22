namespace BunkFy.Host.ServiceDefaults.Observability;

using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;

internal sealed class PrivacyPreservingActivityProcessor : BaseProcessor<Activity>
{
    private const int MaximumErrorTypeLength = 128;

    private static readonly Regex SafeErrorTypePattern = new(
        "^[A-Za-z0-9._:+-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedHttpTags = new(StringComparer.Ordinal)
    {
        "error.type",
        "http.flavor",
        "http.method",
        "http.request.body.size",
        "http.request.method",
        "http.response.body.size",
        "http.response.status_code",
        "http.route",
        "http.status_code",
        "network.protocol.name",
        "network.protocol.version",
        "server.port",
        "url.scheme",
    };

    private static readonly HashSet<string> SensitiveTags = new(StringComparer.Ordinal)
    {
        "client.address",
        "enduser.id",
        "enduser.role",
        "enduser.scope",
        "exception.message",
        "exception.stacktrace",
        "http.client_ip",
        "http.path",
        "http.target",
        "http.url",
        "net.peer.ip",
        "net.sock.host.addr",
        "net.sock.peer.addr",
        "network.local.address",
        "network.peer.address",
        "server.address",
        "session.id",
        "url.full",
        "url.path",
        "url.query",
        "user.id",
    };

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        KeyValuePair<string, object?>[] tags = data.TagObjects.ToArray();
        bool isHttpActivity = IsHttpActivity(data, tags);

        foreach ((string key, object? value) in tags)
        {
            if (ShouldRemoveTag(key, value, isHttpActivity))
            {
                data.SetTag(key, value: null);
            }
        }

        foreach ((string key, _) in data.Baggage.ToArray())
        {
            data.SetBaggage(key, value: null);
        }

        if (isHttpActivity)
        {
            data.DisplayName = GetSafeHttpDisplayName(data);
        }
    }

    private static bool IsHttpActivity(
        Activity activity,
        IReadOnlyCollection<KeyValuePair<string, object?>> tags) =>
        activity.Source.Name is "Microsoft.AspNetCore" or "System.Net.Http"
        || tags.Any(tag => tag.Key.StartsWith("http.", StringComparison.Ordinal)
                           || tag.Key.StartsWith("url.", StringComparison.Ordinal));

    private static bool ShouldRemoveTag(string key, object? value, bool isHttpActivity)
    {
        if (SensitiveTags.Contains(key)
            || key.Contains(".request.header.", StringComparison.Ordinal)
            || key.Contains(".response.header.", StringComparison.Ordinal))
        {
            return true;
        }

        if (!isHttpActivity)
        {
            return false;
        }

        if (!AllowedHttpTags.Contains(key))
        {
            return true;
        }

        return key == "error.type" && !IsSafeErrorType(value);
    }

    private static bool IsSafeErrorType(object? value) =>
        value is string errorType
        && errorType.Length is > 0 and <= MaximumErrorTypeLength
        && SafeErrorTypePattern.IsMatch(errorType);

    private static string GetSafeHttpDisplayName(Activity activity)
    {
        string? method = GetStringTag(activity, "http.request.method")
                         ?? GetStringTag(activity, "http.method");
        string? route = GetStringTag(activity, "http.route");

        if (!string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(route))
        {
            return $"{method} {route}";
        }

        return string.IsNullOrWhiteSpace(method) ? "HTTP" : method;
    }

    private static string? GetStringTag(Activity activity, string key) =>
        activity.GetTagItem(key) as string;
}
