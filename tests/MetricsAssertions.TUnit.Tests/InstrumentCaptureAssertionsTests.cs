using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>
/// Tests for the <see cref="InstrumentCapture"/> adapter assertions (`HasCounterTotal`,
/// `HasCounterTotalAtLeast`, `HasUpDownCounterValue`, `HasMeasurementCount`, `HasNoMeasurements`,
/// `HasLastValue`, `HasTaggedMeasurement`): each passes on a match and raises an
/// <see cref="AssertionException"/> on a mismatch.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class InstrumentCaptureAssertionsTests
{
    [Test]
    public async Task CounterCountAndLastValue_PassAndFail(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.IC.Counter");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);
        counter.Add(3);
        counter.Add(5);

        await Assert.That(capture).HasCounterTotal(8);
        await Assert.That(capture).HasCounterTotalAtLeast(8);
        await Assert.That(capture).HasUpDownCounterValue(8);
        await Assert.That(capture).HasMeasurementCount(2);
        await Assert.That(capture).HasLastValue(5);

        await Assert.That(async () => await Assert.That(capture).HasCounterTotal(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasCounterTotalAtLeast(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasUpDownCounterValue(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasMeasurementCount(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasLastValue(99)).Throws<AssertionException>();
    }

    [Test]
    public async Task HasLastValue_WithTolerance_PassFailAndValidate(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.IC.Tolerance");
        var histogram = meter.CreateHistogram<double>("computed");
        using var capture = InstrumentCapture.Of(histogram);
        histogram.Record(0.1 + 0.2);   // last value 0.30000000000000004

        await Assert.That(capture).HasLastValue(0.3, 1e-9);
        await Assert.That(async () => await Assert.That(capture).HasLastValue(0.3, 0d)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasLastValue(0.5, 1e-9)).Throws<AssertionException>();

        await Assert.That(() => InstrumentCaptureAssertions.HasLastValue(null!, 0.3, 1e-9)).Throws<ArgumentNullException>();
        await Assert.That(() => InstrumentCaptureAssertions.HasLastValue(capture, 0.3, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => InstrumentCaptureAssertions.HasLastValue(capture, 0.3, double.NaN)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task HasLastValue_WithTolerance_EmptyCaptureFails(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.IC.ToleranceEmpty");
        var histogram = meter.CreateHistogram<double>("computed");
        using var capture = InstrumentCapture.Of(histogram);

        // No measurements: the tolerant overload still fails, naming the empty capture.
        await Assert.That(async () => await Assert.That(capture).HasLastValue(0.3, 1e-9)).Throws<AssertionException>();
    }

    [Test]
    public async Task HasNoMeasurementsAndHasLastValue_EmptyCapture(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.IC.Empty");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);

        // empty: no measurements, no last value
        await Assert.That(capture).HasNoMeasurements();
        await Assert.That(async () => await Assert.That(capture).HasLastValue(1)).Throws<AssertionException>();

        counter.Add(1);
        await Assert.That(async () => await Assert.That(capture).HasNoMeasurements()).Throws<AssertionException>();
    }

    [Test]
    public async Task HasTaggedMeasurement_PassAndFail(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.IC.Tagged");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);
        counter.Add(1, new KeyValuePair<string, object?>("route", "/orders"));

        await Assert.That(capture).HasTaggedMeasurement("route", "/orders");
        await Assert.That(async () => await Assert.That(capture).HasTaggedMeasurement("route", "/missing")).Throws<AssertionException>();
    }

    [Test]
    public async Task HasUpDownCounterValue_RealUpDownCounter(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.IC.UpDown");
        var udc = meter.CreateUpDownCounter<long>("connections");
        using var capture = InstrumentCapture.Of(udc);
        udc.Add(5);
        udc.Add(-2);

        await Assert.That(capture).HasUpDownCounterValue(3);
        await Assert.That(async () => await Assert.That(capture).HasUpDownCounterValue(99)).Throws<AssertionException>();

        // HasCounterTotal and HasCounterTotalAtLeast reject the negative delta a real counter could never produce
        await Assert.That(async () => await Assert.That(capture).HasCounterTotal(3)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasCounterTotalAtLeast(1)).Throws<AssertionException>();
    }

    [Test]
    public async Task FailureMessage_DumpsCapturedMeasurements(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.IC.Dump");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);
        counter.Add(3, new KeyValuePair<string, object?>("route", "/orders"));

        AssertionException? caught = null;
        try
        {
            await Assert.That(capture).HasMeasurementCount(5);
        }
        catch (AssertionException e)
        {
            caught = e;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("captured:");
        await Assert.That(caught.Message).Contains("requests = 3");
        await Assert.That(caught.Message).Contains("route=/orders");
    }
}
