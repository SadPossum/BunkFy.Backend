namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using BunkFy.Modules.Ingestion.Application.Ingress;
using BunkFy.Modules.Ingestion.Application.Ports;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterIngressMetricsTests
{
    [Fact]
    public void Admission_counter_uses_only_bounded_operation_and_outcome_tags()
    {
        ConcurrentQueue<IReadOnlyDictionary<string, string>> measurements = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == AdapterIngressMetrics.MeterName &&
                instrument.Name == AdapterIngressMetrics.CounterName)
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            Dictionary<string, string> snapshot = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                snapshot[tag.Key] = tag.Value?.ToString() ?? string.Empty;
            }

            measurements.Enqueue(snapshot);
        });
        listener.Start();

        AdapterIngressMetrics.Record(
            AdapterIngressOperation.RemoteObservation,
            AdapterIngressGateOutcome.QuotaRejected);

        IReadOnlyDictionary<string, string>[] captured = measurements.ToArray();
        Assert.All(
            captured,
            measurement => Assert.Equal(
                ["operation", "outcome"],
                measurement.Keys.Order(StringComparer.Ordinal)));
        IReadOnlyDictionary<string, string> measurement = Assert.Single(
            captured,
            candidate =>
                candidate["operation"] == "remote-observation" &&
                candidate["outcome"] == "quota-rejected");
        Assert.Equal("remote-observation", measurement["operation"]);
        Assert.Equal("quota-rejected", measurement["outcome"]);
    }
}
