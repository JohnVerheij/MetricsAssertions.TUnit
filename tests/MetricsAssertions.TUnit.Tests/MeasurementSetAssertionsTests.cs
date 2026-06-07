using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>
/// Tests for the <see cref="MeasurementSet"/> adapter assertions (counter totals, measurement counts,
/// emptiness, histogram sample sum / average / range, exact and order-insensitive sample sets, and
/// tag-consistency): each passes on a match and raises an <see cref="AssertionException"/> on a mismatch.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class MeasurementSetAssertionsTests
{
    [Test]
    public async Task HistogramAggregates_PassAndFail(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.MS.Hist");
        var histogram = meter.CreateHistogram<int>("latency");
        using var capture = InstrumentCapture.Of(histogram);
        histogram.Record(10);
        histogram.Record(20);
        histogram.Record(30);
        MeasurementSet set = capture.Measurements;

        await Assert.That(set).HasCounterTotal(60);
        await Assert.That(set).HasCounterTotalAtLeast(60);
        await Assert.That(set).HasMeasurementCount(3);
        await Assert.That(set).HasSampleSum(60);
        await Assert.That(set).HasSampleSum(61, 2);
        await Assert.That(set).HasSampleAverage(20);
        await Assert.That(set).HasAllSamplesInRange(0, 100);
        await Assert.That(set).HasSamples(10, 20, 30);
        await Assert.That(set).HasSamplesInAnyOrder(30, 10, 20);

        await Assert.That(async () => await Assert.That(set).HasCounterTotal(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(set).HasCounterTotalAtLeast(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(set).HasMeasurementCount(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(set).HasSampleSum(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(set).HasSampleAverage(99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(set).HasAllSamplesInRange(0, 25)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(set).HasSamples(1, 2, 3)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(set).HasSamplesInAnyOrder(1, 2, 3)).Throws<AssertionException>();
    }

    [Test]
    public async Task HasNoMeasurements_PassAndFail(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.MS.Empty");
        var counter = meter.CreateCounter<long>("c");
        using var capture = InstrumentCapture.Of(counter);

        await Assert.That(capture.Measurements).HasNoMeasurements();

        counter.Add(1);
        await Assert.That(async () => await Assert.That(capture.Measurements).HasNoMeasurements()).Throws<AssertionException>();
    }

    [Test]
    public async Task TagAssertions_PassAndFail(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.MS.Tags");
        var counter = meter.CreateCounter<long>("requests");
        using var capture = InstrumentCapture.Of(counter);
        counter.Add(1, new KeyValuePair<string, object?>("route", "/x"));
        counter.Add(1, new KeyValuePair<string, object?>("route", "/y"));
        MeasurementSet set = capture.Measurements;

        await Assert.That(set).HasEveryMeasurementTagged("route");
        await Assert.That(set).HasTaggedMeasurement("route", "/x");

        await Assert.That(async () => await Assert.That(set).HasEveryMeasurementTagged("verb")).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(set).HasTaggedMeasurement("route", "/z")).Throws<AssertionException>();
    }
}
