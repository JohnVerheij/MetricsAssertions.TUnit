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
    private static readonly double[] InstanceClockSamples = [1.5d];

    [Test]
    public async Task For_WithMeterScope_CapturesScopedMeter(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // A scoped meter is what IMeterFactory (the ASP.NET Core DI path) produces; For(name, scope)
        // captures it, where the default For(name) (no scope) would capture nothing.
        var scope = new object();
        using var meter = new Meter(new MeterOptions("MetricsAssertions.Tests.MeterScoped") { Scope = scope });
        var counter = meter.CreateCounter<long>("hits");

        using var capture = MeterCapture.For("MetricsAssertions.Tests.MeterScoped", scope).Add<long>("hits");
        counter.Add(4);

        await Assert.That(capture.CounterTotal("hits")).IsEqualTo(4L);
    }

    [Test]
    public async Task Add_Instrument_ChecksMeterScope(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Scope is part of the bundle's identity, so the by-reference Add must reject an instrument
        // whose meter shares the name but has a different scope, and accept a matching-scope one.
        var scope = new object();
        using var scoped = new Meter(new MeterOptions("MetricsAssertions.Tests.ScopeGuard") { Scope = scope });
        using var otherScope = new Meter(new MeterOptions("MetricsAssertions.Tests.ScopeGuard") { Scope = new object() });
        var matching = scoped.CreateCounter<long>("hits");
        var mismatched = otherScope.CreateCounter<long>("hits");

        using var capture = MeterCapture.For("MetricsAssertions.Tests.ScopeGuard", scope);
        await Assert.That(() => capture.Add(mismatched)).Throws<ArgumentException>();

        capture.Add(matching);
        matching.Add(3);
        await Assert.That(capture.CounterTotal("hits")).IsEqualTo(3L);
    }

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
        await Assert.That(() => MeterCapture.For((string)null!)).Throws<ArgumentNullException>();
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

    [Test]
    public async Task For_WithTimeProvider_StampsEveryBundledInstrument(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // The clock passed to For applies to instruments added by name and by reference alike.
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        using var meter = new Meter("MetricsAssertions.Tests.CaptureClock");
        var placed = meter.CreateCounter<long>("placed");
        var latency = meter.CreateHistogram<int>("latency");

        using var capture = MeterCapture.For("MetricsAssertions.Tests.CaptureClock", clock)
            .Add<long>("placed")
            .Add(latency);
        placed.Add(1);
        latency.Record(5);

        await Assert.That(capture.Measurements.Count).IsEqualTo(2);
        foreach (CapturedMeasurement measurement in capture.Measurements.All)
            await Assert.That(measurement.Timestamp).IsEqualTo(clock.Now);
    }

    [Test]
    public async Task For_WithScopeAndTimeProvider_CapturesScopedMeterOnTheClock(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 1, 13, 0, 0, TimeSpan.Zero));
        var scope = new object();
        using var meter = new Meter(new MeterOptions("MetricsAssertions.Tests.ScopedClock") { Scope = scope });
        var counter = meter.CreateCounter<long>("hits");

        using var capture = MeterCapture.For("MetricsAssertions.Tests.ScopedClock", scope, clock).Add<long>("hits");
        counter.Add(2);

        await Assert.That(capture.CounterTotal("hits")).IsEqualTo(2L);
        await Assert.That(capture.Measurements.All[0].Timestamp).IsEqualTo(clock.Now);
    }

    [Test]
    public async Task For_WithNullTimeProvider_CapturesUnscopedMeterOnTheSystemClock(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // A literal null binds to the TimeProvider overload and means "no clock", so it still captures the
        // directly-created (unscoped) meter, as For(name, null) did before the clock overload existed.
        using var meter = new Meter("MetricsAssertions.Tests.NullClock");
        var counter = meter.CreateCounter<long>("hits");
        DateTimeOffset before = DateTimeOffset.UtcNow;

        using var capture = MeterCapture.For("MetricsAssertions.Tests.NullClock", null).Add<long>("hits");
        counter.Add(1);

        await Assert.That(capture.CounterTotal("hits")).IsEqualTo(1L);
        await Assert.That(capture.Measurements.All[0].Timestamp).IsGreaterThanOrEqualTo(before);
    }

    [Test]
    public async Task For_TimeProviderPassedAsScope_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // A clock typed as object reaches the scope overload; the capture would match no meter, so it throws.
        object clockAsObject = new FixedTimeProvider(DateTimeOffset.UnixEpoch);

        await Assert.That(() => MeterCapture.For("MetricsAssertions.Tests.ClockAsScope", clockAsObject))
            .Throws<ArgumentException>().WithMessageContaining("TimeProvider", StringComparison.Ordinal);
        await Assert.That(() => MeterCapture.For("MetricsAssertions.Tests.ClockAsScope", clockAsObject, timeProvider: null))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task For_MeterPassedAsScope_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.MeterAsScope");
        object meterAsObject = meter;

        await Assert.That(() => MeterCapture.For("MetricsAssertions.Tests.MeterAsScope", meterAsObject))
            .Throws<ArgumentException>().WithMessageContaining("Meter", StringComparison.Ordinal);
    }

    [Test]
    public async Task For_Meter_CapturesOnlyThatMeterInstance(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Two meters with the same name and scope, as two tests running in parallel would create: capture by
        // meter instance sees only its own meter's measurements.
        using var own = new Meter("MetricsAssertions.Tests.Instance");
        using var other = new Meter("MetricsAssertions.Tests.Instance");
        var ownHits = own.CreateCounter<long>("hits");
        var otherHits = other.CreateCounter<long>("hits");

        using var capture = MeterCapture.For(own).Add<long>("hits");
        otherHits.Add(5);
        ownHits.Add(2);

        await Assert.That(capture.MeterName).IsEqualTo("MetricsAssertions.Tests.Instance");
        await Assert.That(capture.CounterTotal("hits")).IsEqualTo(2L);
    }

    [Test]
    public async Task For_MeterWithTimeProvider_StampsWithTheClock(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 1, 14, 0, 0, TimeSpan.Zero));
        using var meter = new Meter("MetricsAssertions.Tests.InstanceClock");
        var latency = meter.CreateHistogram<double>("latency");

        using var capture = MeterCapture.For(meter, clock).Add(latency);
        latency.Record(1.5);

        await Assert.That(capture.Samples("latency")).IsEquivalentTo(InstanceClockSamples);
        await Assert.That(capture.Measurements.All[0].Timestamp).IsEqualTo(clock.Now);
    }

    [Test]
    public async Task ForMeter_AddInstrument_FromAnotherInstanceWithTheSameName_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var own = new Meter("MetricsAssertions.Tests.InstanceGuard");
        using var other = new Meter("MetricsAssertions.Tests.InstanceGuard");
        var foreign = other.CreateCounter<long>("hits");

        using var capture = MeterCapture.For(own);

        await Assert.That(() => capture.Add(foreign)).Throws<ArgumentException>().WithMessageContaining("different Meter instance", StringComparison.Ordinal);
    }

    [Test]
    public async Task For_NullMeter_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => MeterCapture.For((Meter)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => MeterCapture.For((Meter)null!, timeProvider: null)).Throws<ArgumentNullException>();
    }
}
