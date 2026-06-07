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
}
