using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Tests for <see cref="MeterCapture"/>: building a bundle with <see cref="MeterCapture.For"/> +
/// the by-name and by-reference <c>Add</c> overloads, the per-instrument indexer, the meter-wide
/// <see cref="MeterCapture.Measurements"/>, the per-instrument accessors
/// (<see cref="MeterCapture.CounterTotal"/> / <see cref="MeterCapture.Samples"/> /
/// <see cref="MeterCapture.MeasurementCount"/> / <see cref="MeterCapture.HasMeasurementTagged"/>),
/// <see cref="MeterCapture.RecordObservable"/>, disposal, and the null-argument guards.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class MeterCaptureTests
{
    private static readonly double[] LatencySamples = [50d];

    [Test]
    public async Task Bundle_CapturesAcrossInstruments(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Meter");
        var placed = meter.CreateCounter<long>("orders.placed");
        var latency = meter.CreateHistogram<int>("latency");

        using var capture = MeterCapture.For("MetricsAssertions.Tests.Meter")
            .Add<long>("orders.placed")
            .Add(latency);

        placed.Add(2, new KeyValuePair<string, object?>("region", "eu"));
        placed.Add(3);
        latency.Record(50);

        await Assert.That(capture.MeterName).IsEqualTo("MetricsAssertions.Tests.Meter");
        await Assert.That(capture.CounterTotal("orders.placed")).IsEqualTo(5L);
        await Assert.That(capture.MeasurementCount("orders.placed")).IsEqualTo(2);
        await Assert.That(capture.Samples("latency")).IsEquivalentTo(LatencySamples);
        await Assert.That(capture.HasMeasurementTagged("orders.placed", "region", "eu")).IsTrue();
        await Assert.That(capture.HasMeasurementTagged("orders.placed", "region", "us")).IsFalse();
        await Assert.That(capture["latency"].InstrumentName).IsEqualTo("latency");
        await Assert.That(capture.Measurements.Count).IsEqualTo(3);
    }

    [Test]
    public async Task RecordObservable_PullsBundledGauges(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.MeterGauge");
        _ = meter.CreateObservableGauge("queue.depth", () => 7L);

        using var capture = MeterCapture.For("MetricsAssertions.Tests.MeterGauge").Add<long>("queue.depth");

        capture.RecordObservable();

        await Assert.That(capture.MeasurementCount("queue.depth")).IsEqualTo(1);
    }

    [Test]
    public async Task For_NullMeterName_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => MeterCapture.For(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AddInstrument_Null_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = MeterCapture.For("MetricsAssertions.Tests.MeterNull");
        await Assert.That(() => capture.Add((Instrument<long>)null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Add_DuplicateName_ReplacesAndDisposesPrior(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Dup");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = MeterCapture.For("MetricsAssertions.Tests.Dup")
            .Add<long>("requests")
            .Add<long>("requests");
        counter.Add(1);
        await Assert.That(capture.MeasurementCount("requests")).IsEqualTo(1);
    }

    [Test]
    public async Task Contains_TrueForBundled_FalseOtherwise(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = MeterCapture.For("MetricsAssertions.Tests.Has").Add<long>("x");
        await Assert.That(capture.Contains("x")).IsTrue();
        await Assert.That(capture.Contains("y")).IsFalse();
    }

    [Test]
    public async Task AddInstrument_FromDifferentMeter_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var other = new Meter("MetricsAssertions.Tests.OtherMeter");
        var foreign = other.CreateCounter<long>("foreign");
        using var capture = MeterCapture.For("MetricsAssertions.Tests.HostMeter");
        await Assert.That(() => capture.Add(foreign)).Throws<ArgumentException>();
    }
}
