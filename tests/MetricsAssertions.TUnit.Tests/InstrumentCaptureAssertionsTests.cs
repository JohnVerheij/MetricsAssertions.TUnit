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
}
