using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Tests for <see cref="InstrumentCapture"/>: capturing a referenceable instrument (<see cref="InstrumentCapture.Of{T}"/>),
/// capturing by meter and instrument name (<see cref="InstrumentCapture.OfName{T}"/>), capturing an observable
/// instrument (<see cref="InstrumentCapture.OfObservable{T}"/> + <see cref="InstrumentCapture.RecordObservable"/>),
/// the convenience surface (<see cref="InstrumentCapture.Count"/> / <see cref="InstrumentCapture.Total"/> /
/// <see cref="InstrumentCapture.LastValue"/> / <see cref="InstrumentCapture.Tagged"/> /
/// <see cref="InstrumentCapture.HasMeasurementTagged"/>), the baseline delta
/// (<see cref="InstrumentCapture.Snapshot"/> / <see cref="InstrumentCapture.Since"/>),
/// <see cref="InstrumentCapture.WaitForAsync"/>, and the null-argument guard.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class InstrumentCaptureTests
{
    [Test]
    public async Task Of_CapturesRecordedMeasurements(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Of");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(3);
        counter.Add(5);

        await Assert.That(capture.InstrumentName).IsEqualTo("requests");
        await Assert.That(capture.Count).IsEqualTo(2);
        await Assert.That(capture.Total).IsEqualTo(8L);
        await Assert.That(capture.LastValue).IsEqualTo(5d);
        await Assert.That(capture.Measurements.All[0].Value).IsEqualTo(3d);
        await Assert.That(capture.Measurements.All[0].InstrumentName).IsEqualTo("requests");
    }

    [Test]
    public async Task OfName_WithMeterScope_CapturesScopedMeterThatNullScopeMisses(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // A meter created with a scope is what IMeterFactory (the ASP.NET Core DI path) produces.
        var scope = new object();
        using var meter = new Meter(new MeterOptions("MetricsAssertions.Tests.Scoped") { Scope = scope });
        var counter = meter.CreateCounter<long>("hits");

        using var unscoped = InstrumentCapture.OfName<long>("MetricsAssertions.Tests.Scoped", "hits");
        using var scoped = InstrumentCapture.OfName<long>(scope, "MetricsAssertions.Tests.Scoped", "hits");
        counter.Add(7);

        // The no-scope overload silently captures nothing from a scoped meter; the scope-aware one works.
        await Assert.That(unscoped.Count).IsEqualTo(0);
        await Assert.That(scoped.Count).IsEqualTo(1);
        await Assert.That(scoped.Total).IsEqualTo(7L);
    }

    [Test]
    public async Task OfName_CapturesByMeterAndInstrumentName(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.ByName");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = InstrumentCapture.OfName<long>("MetricsAssertions.Tests.ByName", "hits");

        counter.Add(2);

        await Assert.That(capture.Count).IsEqualTo(1);
        await Assert.That(capture.Total).IsEqualTo(2L);
    }

    [Test]
    public async Task OfObservable_RecordsOnDemand(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Gauge");
        var gauge = meter.CreateObservableGauge("temperature", () => 42L);
        using var capture = InstrumentCapture.OfObservable(gauge);

        capture.RecordObservable();

        await Assert.That(capture.Count).IsEqualTo(1);
        await Assert.That(capture.LastValue).IsEqualTo(42d);
    }

    [Test]
    public async Task TaggedAndHasMeasurementTagged_FilterByTag(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Tags");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(1, new KeyValuePair<string, object?>("route", "/orders"));
        counter.Add(1, new KeyValuePair<string, object?>("route", "/health"));

        await Assert.That(capture.Tagged("route", "/orders").Count).IsEqualTo(1);
        await Assert.That(capture.HasMeasurementTagged("route", "/orders")).IsTrue();
        await Assert.That(capture.HasMeasurementTagged("route", "/missing")).IsFalse();
    }

    [Test]
    public async Task SnapshotAndSince_ReturnTheDelta(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Delta");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(10);
        MeasurementBaseline baseline = capture.Snapshot();
        counter.Add(4);

        await Assert.That(capture.Since(baseline).Count).IsEqualTo(1);
        await Assert.That(capture.Since(baseline).Total).IsEqualTo(4L);
    }

    [Test]
    public async Task WaitForAsync_CompletesOnceCountReached(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Wait");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(1);
        counter.Add(1);

        await capture.WaitForAsync(2, ct);
        await Assert.That(capture.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Of_NullInstrument_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => InstrumentCapture.Of<long>(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Since_WithForeignBaseline_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Foreign");
        var counter = meter.CreateCounter<long>("requests");
        using var captureA = InstrumentCapture.Of(counter);
        using var captureB = InstrumentCapture.Of(counter);
        MeasurementBaseline baseline = captureA.Snapshot();
        await Assert.That(() => captureB.Since(baseline)).Throws<ArgumentException>();
    }

    [Test]
    public async Task OfName_Meter_CapturesOnlyThatMeterInstance(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Same meter name and scope on two instances: the meter-instance capture ignores the other one.
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 1, 15, 0, 0, TimeSpan.Zero));
        using var own = new Meter("MetricsAssertions.Tests.OfNameInstance");
        using var other = new Meter("MetricsAssertions.Tests.OfNameInstance");
        var ownRequests = own.CreateCounter<long>("requests");
        var otherRequests = other.CreateCounter<long>("requests");

        using var capture = InstrumentCapture.OfName<long>(own, "requests", clock);
        otherRequests.Add(10);
        ownRequests.Add(3);

        await Assert.That(capture.Total).IsEqualTo(3L);
        await Assert.That(capture.Measurements.All[0].Timestamp).IsEqualTo(clock.Now);
    }

    [Test]
    public async Task OfName_Meter_NullArguments_Throw(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.OfNameNull");
        await Assert.That(() => InstrumentCapture.OfName<long>((Meter)null!, "requests")).Throws<ArgumentNullException>();
        await Assert.That(() => InstrumentCapture.OfName<long>(meter, null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task OfName_TimeProviderOrMeterPassedAsScope_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Values typed as object reach the scope overload; neither is ever a meter scope.
        using var meter = new Meter("MetricsAssertions.Tests.OfNameBadScope");
        object clockAsObject = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        object meterAsObject = meter;

        await Assert.That(() => InstrumentCapture.OfName<long>(clockAsObject, "MetricsAssertions.Tests.OfNameBadScope", "requests"))
            .Throws<ArgumentException>().WithMessageContaining("TimeProvider", StringComparison.Ordinal);
        await Assert.That(() => InstrumentCapture.OfName<long>(meterAsObject, "MetricsAssertions.Tests.OfNameBadScope", "requests"))
            .Throws<ArgumentException>().WithMessageContaining("Meter", StringComparison.Ordinal);
    }
}
