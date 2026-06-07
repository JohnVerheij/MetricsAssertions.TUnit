using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Tests for the framework-agnostic <see cref="InstrumentCapture"/>: capturing the measurements a
/// referenceable instrument records via <see cref="InstrumentCapture.Of{T}"/>, the
/// <see cref="InstrumentCapture.Measurements"/> snapshot (with values projected to <c>double</c> and the
/// originating instrument name), <see cref="InstrumentCapture.Count"/>, and the null-argument guard.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class InstrumentCaptureTests
{
    [Test]
    public async Task Of_CapturesRecordedMeasurements(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Capture");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(3);
        counter.Add(5);

        await Assert.That(capture.InstrumentName).IsEqualTo("requests");
        await Assert.That(capture.Count).IsEqualTo(2);
        await Assert.That(capture.Measurements[0].Value).IsEqualTo(3d);
        await Assert.That(capture.Measurements[1].Value).IsEqualTo(5d);
        await Assert.That(capture.Measurements[0].InstrumentName).IsEqualTo("requests");
    }

    [Test]
    public async Task Of_CarriesMeasurementTags(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Tags");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(1, new KeyValuePair<string, object?>("route", "/orders"));

        await Assert.That(capture.Count).IsEqualTo(1);
        await Assert.That(capture.Measurements[0].Tags["route"]).IsEqualTo("/orders");
    }

    [Test]
    public async Task Of_NullInstrument_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => InstrumentCapture.Of<long>(null!)).Throws<ArgumentNullException>();
    }
}
