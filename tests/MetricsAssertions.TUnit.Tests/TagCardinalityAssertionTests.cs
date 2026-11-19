using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>
/// Tests for <c>HasTagCardinalityAtMost</c> and <c>HasSeriesCountAtMost</c> on all three receivers: the decision table
/// (below, at and above the budget; the tag on some, none or no measurements), the argument checks, and the assertions where
/// a suite really calls them (an id leaking into a tag, a flow-scoped capture of a static meter that tests running in
/// parallel emit to, a chain with <c>And</c>, <c>Because</c>).
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class TagCardinalityAssertionTests
{
    private const string StaticMeterName = "MetricsAssertions.TUnit.Tests.Cardinality.Static";

    private static readonly Meter StaticMeter = new(StaticMeterName);
    private static readonly Counter<long> StaticOrders = StaticMeter.CreateCounter<long>("orders.placed");

    private static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);

    private static readonly string[] OneA = ["a"];
    private static readonly string[] BAndC = ["b", "c"];
    private static readonly int[] IntsOneTwo = [1, 2];

    private static CapturedMeasurement M(string instrument, params (string Key, object? Value)[] tags)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in tags)
            dict[k] = v;
        return new CapturedMeasurement(instrument, 1d, dict, DateTimeOffset.UnixEpoch);
    }

    private static readonly MeasurementSet ThreeTenants = new(
    [
        M("orders.placed", ("tenant", "a")),
        M("orders.placed", ("tenant", "b")),
        M("orders.placed"),
        M("orders.placed", ("tenant", "a")),
        M("orders.placed", ("tenant", "c")),
    ]);

    [Test]
    public async Task MeasurementSet_TagCardinality_DecisionTable(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(ThreeTenants).HasTagCardinalityAtMost("tenant", 4);
        await Assert.That(ThreeTenants).HasTagCardinalityAtMost("tenant", 3);
        await Assert.That(async () => await Assert.That(ThreeTenants).HasTagCardinalityAtMost("tenant", 2)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(ThreeTenants).HasTagCardinalityAtMost("region", 5)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(MeasurementSet.Empty).HasTagCardinalityAtMost("tenant", 5)).Throws<AssertionException>();
    }

    [Test]
    public async Task MeasurementSet_SeriesCount_DecisionTable(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // a, b, (no tag), c: four series.
        await Assert.That(ThreeTenants).HasSeriesCountAtMost(5);
        await Assert.That(ThreeTenants).HasSeriesCountAtMost(4);
        await Assert.That(async () => await Assert.That(ThreeTenants).HasSeriesCountAtMost(3)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(MeasurementSet.Empty).HasSeriesCountAtMost(5)).Throws<AssertionException>();
    }

    [Test]
    public async Task ArrayTagValues_CountAsDistinctValues(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var arrays = new MeasurementSet(
        [
            M("x", ("k", OneA)),
            M("x", ("k", BAndC)),
            M("x", ("k", IntsOneTwo)),
        ]);

        await Assert.That(arrays).HasTagCardinalityAtMost("k", 3);
        await Assert.That(async () => await Assert.That(arrays).HasTagCardinalityAtMost("k", 2)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(arrays).HasSeriesCountAtMost(2)).Throws<AssertionException>();
    }

    [Test]
    public async Task InstrumentCapture_PassesAndFails(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Cardinality.IC");
        var counter = meter.CreateCounter<long>("orders.placed");
        using var capture = InstrumentCapture.Of(counter);
        using var empty = InstrumentCapture.Of(meter.CreateCounter<long>("orders.none"));
        counter.Add(1, Tag("tenant", "a"), Tag("region", "eu"));
        counter.Add(1, Tag("tenant", "b"), Tag("region", "eu"));

        await Assert.That(capture).HasTagCardinalityAtMost("tenant", 2);
        await Assert.That(capture).HasTagCardinalityAtMost("region", 1);
        await Assert.That(capture).HasSeriesCountAtMost(2);
        await Assert.That(async () => await Assert.That(capture).HasTagCardinalityAtMost("tenant", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasTagCardinalityAtMost("route", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasSeriesCountAtMost(1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(empty).HasTagCardinalityAtMost("tenant", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(empty).HasSeriesCountAtMost(1)).Throws<AssertionException>();
    }

    [Test]
    public async Task MeterCapture_CountsTheNamedInstrumentOnly(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Cardinality.MC");
        var orders = meter.CreateCounter<long>("orders.placed");
        var latency = meter.CreateHistogram<double>("orders.latency");
        using var capture = MeterCapture.For(meter).Add<long>("orders.placed").Add<double>("orders.latency").Add<long>("orders.none");
        orders.Add(1, Tag("tenant", "a"));
        latency.Record(1, Tag("tenant", "b"));
        latency.Record(1, Tag("tenant", "c"));

        await Assert.That(capture).HasTagCardinalityAtMost("orders.placed", "tenant", 1);
        await Assert.That(capture).HasSeriesCountAtMost("orders.placed", 1);
        await Assert.That(capture).HasTagCardinalityAtMost("orders.latency", "tenant", 2);
        await Assert.That(capture.Measurements).HasTagCardinalityAtMost("tenant", 3);
        await Assert.That(capture.Measurements).HasSeriesCountAtMost(3);
        await Assert.That(async () => await Assert.That(capture).HasTagCardinalityAtMost("orders.latency", "tenant", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasSeriesCountAtMost("orders.latency", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasTagCardinalityAtMost("orders.none", "tenant", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasSeriesCountAtMost("orders.none", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasTagCardinalityAtMost("missing", "tenant", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasSeriesCountAtMost("missing", 1)).Throws<AssertionException>();
    }

    [Test]
    public async Task ArgumentChecks(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Cardinality.Args");
        using var capture = InstrumentCapture.Of(meter.CreateCounter<long>("c"));
        using var bundle = MeterCapture.For(meter).Add<long>("c");
        MeasurementSet set = ThreeTenants;

        await Assert.That(() => MeasurementSetAssertions.HasTagCardinalityAtMost(null!, "k", 1)).Throws<ArgumentNullException>();
        await Assert.That(() => MeasurementSetAssertions.HasTagCardinalityAtMost(set, null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => MeasurementSetAssertions.HasTagCardinalityAtMost(set, "k", 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => MeasurementSetAssertions.HasTagCardinalityAtMost(set, "k", -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => MeasurementSetAssertions.HasSeriesCountAtMost(null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => MeasurementSetAssertions.HasSeriesCountAtMost(set, 0)).Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => InstrumentCaptureAssertions.HasTagCardinalityAtMost(null!, "k", 1)).Throws<ArgumentNullException>();
        await Assert.That(() => InstrumentCaptureAssertions.HasTagCardinalityAtMost(capture, null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => InstrumentCaptureAssertions.HasTagCardinalityAtMost(capture, "k", 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => InstrumentCaptureAssertions.HasSeriesCountAtMost(null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => InstrumentCaptureAssertions.HasSeriesCountAtMost(capture, 0)).Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => MeterCaptureAssertions.HasTagCardinalityAtMost(null!, "c", "k", 1)).Throws<ArgumentNullException>();
        await Assert.That(() => MeterCaptureAssertions.HasTagCardinalityAtMost(bundle, null!, "k", 1)).Throws<ArgumentNullException>();
        await Assert.That(() => MeterCaptureAssertions.HasTagCardinalityAtMost(bundle, "c", null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => MeterCaptureAssertions.HasTagCardinalityAtMost(bundle, "missing", "k", 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => MeterCaptureAssertions.HasSeriesCountAtMost(null!, "c", 1)).Throws<ArgumentNullException>();
        await Assert.That(() => MeterCaptureAssertions.HasSeriesCountAtMost(bundle, null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => MeterCaptureAssertions.HasSeriesCountAtMost(bundle, "missing", 0)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AnOrderIdLeakingIntoATag_FailsTheBudget_WithBecause(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Cardinality.Leak");
        var placed = meter.CreateCounter<long>("orders.placed");
        using var capture = InstrumentCapture.Of(placed);

        // The bug: the order id instead of the tenant id is passed as the tenant tag.
        for (int order = 0; order < 50; order++)
            placed.Add(1, Tag("tenant", $"order-{order}"));

        var exception = await Assert.That(async () => await Assert.That(capture).HasTagCardinalityAtMost("tenant", 3)
            .Because("tenants are a closed set")).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("tenants are a closed set");
        await Assert.That(exception.Message).Contains("but it had 50\n  values:\n    \"order-0\"");
        await Assert.That(exception.Message).Contains("    ... and 30 more\n  captured:");
    }

    [Test]
    public async Task ChainedWithAnd_BothBudgetsAreChecked(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(ThreeTenants).HasTagCardinalityAtMost("tenant", 3).And.HasSeriesCountAtMost(4);
        await Assert.That(async () => await Assert.That(ThreeTenants).HasTagCardinalityAtMost("tenant", 3).And.HasSeriesCountAtMost(3))
            .Throws<AssertionException>();
    }

    [Test]
    [Arguments("tenant-1")]
    [Arguments("tenant-2")]
    [Arguments("tenant-3")]
    [Arguments("tenant-4")]
    public async Task FlowScopedCaptureOfAStaticMeter_SeesOnlyThisTestsTenant(string tenant, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // The four cases run in parallel and emit to one static counter; each capture keeps only its own flow.
        using var capture = MeterCapture.For(StaticMeterName).InCurrentFlow().Add<long>("orders.placed");
        for (int i = 0; i < 200; i++)
        {
            StaticOrders.Add(1, Tag("tenant", tenant));
            if (i % 50 == 0)
                await Task.Yield();
        }

        await Assert.That(capture).HasTagCardinalityAtMost("orders.placed", "tenant", 1);
        await Assert.That(capture).HasSeriesCountAtMost("orders.placed", 1);
        await Assert.That(capture.Measurements.DistinctTagValues("tenant")).IsEquivalentTo([tenant]);
    }
}
