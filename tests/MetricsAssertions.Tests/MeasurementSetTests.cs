using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Tests for <see cref="MeasurementSet"/>: the aggregates (<see cref="MeasurementSet.Total"/> /
/// <see cref="MeasurementSet.Sum"/> / <see cref="MeasurementSet.Min"/> / <see cref="MeasurementSet.Max"/> /
/// <see cref="MeasurementSet.Average"/> / <see cref="MeasurementSet.LastValue"/>), the empty handling
/// (<see cref="MeasurementSet.Empty"/> / <see cref="MeasurementSet.IsEmpty"/>), the sample comparisons
/// (<see cref="MeasurementSet.SamplesEqual"/> / <see cref="MeasurementSet.SamplesEquivalentTo"/>), the
/// narrowing (<see cref="MeasurementSet.Tagged(ValueTuple{string, object}[])"/> /
/// <see cref="MeasurementSet.ForInstrument"/>), the tag and range predicates, and the deterministic
/// <see cref="MeasurementSet.ToSnapshotString"/> projection.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class MeasurementSetTests
{
    private static readonly double[] HistogramValues = [10d, 20d, 30d];

    private static MeasurementSet HistogramSet(out Meter meter)
    {
        meter = new Meter("MetricsAssertions.Tests.Set");
        var histogram = meter.CreateHistogram<int>("latency");
        using var capture = InstrumentCapture.Of(histogram);
        histogram.Record(10);
        histogram.Record(20);
        histogram.Record(30);
        return capture.Measurements;
    }

    private static CapturedMeasurement M(string instrument, double value, params (string Key, object? Value)[] tags)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in tags)
            dict[k] = v;
        return new CapturedMeasurement(instrument, value, dict, DateTimeOffset.UnixEpoch);
    }

    [Test]
    public async Task Aggregates_OverHistogramSamples(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        MeasurementSet set = HistogramSet(out var meter);
        using (meter)
        {
            await Assert.That(set.Count).IsEqualTo(3);
            await Assert.That(set.Values).IsEquivalentTo(HistogramValues);
            await Assert.That(set.Sum).IsEqualTo(60d);
            await Assert.That(set.Total).IsEqualTo(60L);
            await Assert.That(set.Min).IsEqualTo(10d);
            await Assert.That(set.Max).IsEqualTo(30d);
            await Assert.That(set.Average).IsEqualTo(20d);
            await Assert.That(set.LastValue).IsEqualTo(30d);
            await Assert.That(set.SamplesEqual(10, 20, 30)).IsTrue();
            await Assert.That(set.SamplesEquivalentTo(30, 10, 20)).IsTrue();
            await Assert.That(set.AllValuesInRange(0, 100)).IsTrue();
            await Assert.That(set.AllValuesInRange(0, 25)).IsFalse();
        }
    }

    [Test]
    public async Task Empty_HasNoMeasurementsAndZeroAggregates(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(MeasurementSet.Empty.IsEmpty).IsTrue();
        await Assert.That(MeasurementSet.Empty.Count).IsEqualTo(0);
        await Assert.That(MeasurementSet.Empty.Min).IsEqualTo(0d);
        await Assert.That(MeasurementSet.Empty.Max).IsEqualTo(0d);
        await Assert.That(MeasurementSet.Empty.Average).IsEqualTo(0d);
        await Assert.That(MeasurementSet.Empty.LastValue).IsNull();
        await Assert.That(MeasurementSet.Empty.EveryMeasurementCarriesTag("x")).IsFalse();
    }

    [Test]
    public async Task TaggedAndForInstrument_Narrow(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(new[]
        {
            M("a", 1, ("route", "/x"), ("verb", "GET")),
            M("a", 2, ("route", "/x"), ("verb", "POST")),
            M("b", 3, ("route", "/y")),
        });

        await Assert.That(set.ForInstrument("a").Count).IsEqualTo(2);
        await Assert.That(set.Tagged("route", "/x").Count).IsEqualTo(2);
        await Assert.That(set.Tagged(("route", "/x"), ("verb", "GET")).Count).IsEqualTo(1);
        await Assert.That(set.EveryMeasurementCarriesTag("route")).IsTrue();
        await Assert.That(set.EveryMeasurementCarriesTag("verb")).IsFalse();
    }

    [Test]
    public async Task ToSnapshotString_IsDeterministic(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(new[]
        {
            M("b", 2, ("k", "v")),
            M("a", 1),
        });

        await Assert.That(set.ToSnapshotString()).IsEqualTo("a 1\nb 2 {k=v}\n");
    }

    [Test]
    public async Task Constructor_NullMeasurements_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => new MeasurementSet(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SamplesEqual_WithTolerance_MatchesPerSample(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Computed doubles: 0.1 + 0.2 is 0.30000000000000004, so exact comparison flakes.
        var set = new MeasurementSet(new[] { M("h", 0.1 + 0.2), M("h", 1.1 + 2.2) });
        double[] expected = [0.3, 3.3];

        await Assert.That(set.SamplesEqual(expected, 1e-9)).IsTrue();
        await Assert.That(set.SamplesEqual(expected, 0d)).IsFalse();          // exact comparison fails
        await Assert.That(set.SamplesEqual([0.3], 1e-9)).IsFalse();           // count mismatch
        await Assert.That(set.SamplesEqual([0.3, 99d], 1e-9)).IsFalse();      // second sample beyond tolerance
        await Assert.That(() => set.SamplesEqual(null!, 1e-9)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SamplesEquivalentTo_WithTolerance_MatchesRegardlessOfOrder(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(new[] { M("h", 0.1 + 0.2), M("h", 1.1 + 2.2) });

        await Assert.That(set.SamplesEquivalentTo([3.3, 0.3], 1e-9)).IsTrue();
        await Assert.That(set.SamplesEquivalentTo([0.3], 1e-9)).IsFalse();        // count mismatch
        await Assert.That(set.SamplesEquivalentTo([0.3, 99d], 1e-9)).IsFalse();   // largest sample beyond tolerance
        await Assert.That(() => set.SamplesEquivalentTo(null!, 1e-9)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SamplesEqual_WithInvalidTolerance_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(new[] { M("h", 1d) });

        // A NaN tolerance would make every per-sample comparison false, so everything matches; a
        // negative tolerance would reject even an exact match. Both fail fast instead.
        await Assert.That(() => set.SamplesEqual([1d], double.NaN)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => set.SamplesEqual([1d], -1e-9)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => set.SamplesEqual([1d], double.PositiveInfinity)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task SamplesEquivalentTo_WithInvalidTolerance_Throws(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(new[] { M("h", 1d) });

        await Assert.That(() => set.SamplesEquivalentTo([1d], double.NaN)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => set.SamplesEquivalentTo([1d], -1e-9)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => set.SamplesEquivalentTo([1d], double.PositiveInfinity)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Describe_RendersMeasurementsAndEmptyPlaceholder(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(MeasurementSet.Empty.Describe()).Contains("no measurements");

        var set = new MeasurementSet(new[] { M("a", 1), M("b", 2, ("k", "v")) });
        string text = set.Describe();
        await Assert.That(text).Contains("a = 1");
        await Assert.That(text).Contains("b = 2 {k=v}");
    }
}
