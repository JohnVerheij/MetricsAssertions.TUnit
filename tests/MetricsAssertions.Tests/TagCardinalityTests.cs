using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;

namespace MetricsAssertions.Tests;

/// <summary>
/// Tests for the tag and series cardinality reads of <see cref="MeasurementSet"/>:
/// <see cref="MeasurementSet.DistinctTagValues"/> and <see cref="MeasurementSet.DistinctSeries"/>, over hand-built sets and
/// over captures as a test suite uses them (an observable gauge pulled repeatedly, a delta after a baseline, a flow-scoped
/// capture next to emitters of other flows, a capture read while an emitter runs).
/// </summary>
[Category("Smoke")]
[Timeout(30_000)]
internal sealed class TagCardinalityTests
{
    private static CapturedMeasurement M(string instrument, params (string Key, object? Value)[] tags)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in tags)
            dict[k] = v;
        return new CapturedMeasurement(instrument, 1d, dict, DateTimeOffset.UnixEpoch);
    }

    private static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);

    private static readonly string[] OneA = ["a"];
    private static readonly string[] BAndC = ["b", "c"];
    private static readonly string[] AAndB = ["a", "b"];
    private static readonly int[] IntsOneTwo = [1, 2];
    private static readonly long[] LongsOneTwo = [1, 2];
    private static readonly string?[] NullAndZ = [null, "z"];
    private static readonly object[] Nested = [new[] { 1.5, 2 }, true];

    [Test]
    public async Task DistinctTagValues_FirstSeenOrder_SkipsMeasurementsWithoutTheTag(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(
        [
            M("orders", ("tenant", "b")),
            M("orders"),
            M("orders", ("tenant", "a")),
            M("orders", ("tenant", "b")),
            M("orders", ("region", "eu")),
        ]);

        await Assert.That(set.DistinctTagValues("tenant")).IsEquivalentTo(["b", "a"], CollectionOrdering.Matching);
        await Assert.That(set.DistinctTagValues("region")).IsEquivalentTo(["eu"]);
        await Assert.That(set.DistinctTagValues("missing")).IsEmpty();
        await Assert.That(set.DistinctTagValues("Tenant")).IsEmpty();
    }

    [Test]
    public async Task DistinctTagValues_CompareAsInvariantText(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
            var set = new MeasurementSet(
            [
                M("http", ("status", 200)),
                M("http", ("status", "200")),
                M("http", ("status", 404L)),
                M("http", ("ok", null)),
                M("http", ("ok", string.Empty)),
                M("http", ("ok", true)),
                M("http", ("ok", "True")),
                M("http", ("ok", "true")),
                M("http", ("ratio", 1.5)),
            ]);

            await Assert.That(set.DistinctTagValues("status")).IsEquivalentTo(["200", "404"], CollectionOrdering.Matching);
            await Assert.That(set.DistinctTagValues("ok")).IsEquivalentTo([string.Empty, "True", "true"], CollectionOrdering.Matching);
            await Assert.That(set.DistinctTagValues("ratio")).IsEquivalentTo(["1.5"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public async Task ArrayTagValues_ReadAsTheirElements_NotAsTheirType(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(
        [
            M("x", ("k", OneA)),
            M("x", ("k", BAndC)),
            M("x", ("k", IntsOneTwo)),
            M("x", ("k", LongsOneTwo)),
            M("x", ("k", Array.Empty<string>())),
            M("x", ("k", NullAndZ)),
            M("x", ("k", Nested)),
        ]);

        await Assert.That(set.DistinctTagValues("k")).IsEquivalentTo(
            ["[a]", "[b, c]", "[1, 2]", "[]", "[, z]", "[[1.5, 2], True]"],
            CollectionOrdering.Matching);
        await Assert.That(set.DistinctSeries()).Count().IsEqualTo(6);
        await Assert.That(set.Tagged("k", BAndC.ToArray()).Count).IsEqualTo(1);
        await Assert.That(set.Tagged("k", AAndB).Count).IsEqualTo(0);
        await Assert.That(set.Tagged("k", LongsOneTwo.ToArray()).Count).IsEqualTo(2);
        await Assert.That(set.ToSnapshotString()).Contains("x 1 {k=[b, c]}\n");
        await Assert.That(set.Describe()).Contains("    x = 1 {k=[b, c]} @ ");
    }

    [Test]
    public async Task ArrayTagValue_ThatContainsItself_ReadsAsAPlaceholderWhereItRepeats(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var self = new object[2];
        self[0] = "a";
        self[1] = self;
        var deeper = new object[] { new object[] { self } };
        var twice = new object[] { OneA, OneA };
        var set = new MeasurementSet(
        [
            M("x", ("k", self)),
            M("x", ("k", deeper)),
            M("x", ("k", twice)),
        ]);

        // Only a repeat inside itself is cut; the same array twice side by side is written twice.
        await Assert.That(set.DistinctTagValues("k")).IsEquivalentTo(
            ["[a, [...]]", "[[[a, [...]]]]", "[[a], [a]]"],
            CollectionOrdering.Matching);
        await Assert.That(set.Tagged("k", self).Count).IsEqualTo(1);
        await Assert.That(set.ToSnapshotString()).Contains("x 1 {k=[a, [...]]}\n");
    }

    [Test]
    public async Task DistinctTagValues_NullKey_Throws_AlsoOnAnEmptySet(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => MeasurementSet.Empty.DistinctTagValues(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => new MeasurementSet([M("a", ("k", "v"))]).DistinctTagValues(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DistinctSeries_TagOrderDoesNotCount_AMissingTagDoes(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Cardinality.Order");
        var counter = meter.CreateCounter<long>("orders.placed");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(1, Tag("tenant", "a"), Tag("region", "eu"));
        counter.Add(1, Tag("region", "eu"), Tag("tenant", "a"));
        counter.Add(1, Tag("tenant", "a"));
        counter.Add(1);
        counter.Add(1);

        await Assert.That(capture.Measurements.DistinctSeries()).IsEquivalentTo(
            ["orders.placed {region=eu,tenant=a}", "orders.placed {tenant=a}", "orders.placed"],
            CollectionOrdering.Matching);
    }

    [Test]
    public async Task DistinctSeries_TellsInstrumentsApart_AndNullFromAMissingTag(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(
        [
            M("a", ("k", "v")),
            M("b", ("k", "v")),
            M("a", ("k", null)),
            M("a", ("k", string.Empty)),
            M("a"),
            M("a", ("k", 1)),
            M("a", ("k", "1")),
        ]);

        await Assert.That(set.DistinctSeries()).IsEquivalentTo(
            ["a {k=v}", "b {k=v}", "a {k=}", "a", "a {k=1}"],
            CollectionOrdering.Matching);
        await Assert.That(MeasurementSet.Empty.DistinctSeries()).IsEmpty();
    }

    [Test]
    public async Task DistinctSeries_TextThatReadsAlike_IsStillTwoSeries(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet(
        [
            M("x", ("a", "1,b=2")),
            M("x", ("a", "1"), ("b", "2")),
            M("x", ("a", "1"), ("b", "2")),
            M("x", ("a=1,b", "2")),
            M("y", ("a", "1")),
            M("y {a=1}"),
        ]);

        // Every pair reads alike; the identity is built from the names, keys and values, not from the entry text.
        await Assert.That(set.DistinctSeries()).IsEquivalentTo(
            ["x {a=1,b=2}", "x {a=1,b=2}", "x {a=1,b=2}", "y {a=1}", "y {a=1}"],
            CollectionOrdering.Matching);
    }

    [Test]
    public async Task DistinctSeries_EscapesLineBreaks_SoEveryEntryStaysOnOneLine(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var set = new MeasurementSet([M("multi\nline", ("path", "C:\\tmp\r\n"))]);

        await Assert.That(set.DistinctSeries()).IsEquivalentTo(["multi\\nline {path=C:\\\\tmp\\r\\n}"]);
        await Assert.That(set.DistinctTagValues("path")).IsEquivalentTo(["C:\\tmp\r\n"]);
    }

    [Test]
    public async Task ObservableGauge_PulledThreeTimes_KeepsOneSeriesPerQueue(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Cardinality.Gauge");
        var depth = 0;
        var gauge = meter.CreateObservableGauge("queue.depth", () => new[]
        {
            new Measurement<int>(++depth, Tag("queue", "orders")),
            new Measurement<int>(depth, Tag("queue", "invoices")),
            new Measurement<int>(depth, Tag("queue", "mail")),
        });
        using var capture = InstrumentCapture.OfObservable(gauge);

        capture.RecordObservable();
        capture.RecordObservable();
        capture.RecordObservable();

        await Assert.That(capture.Count).IsEqualTo(9);
        await Assert.That(capture.Measurements.DistinctSeries()).Count().IsEqualTo(3);
        await Assert.That(capture.Measurements.DistinctTagValues("queue")).IsEquivalentTo(
            ["orders", "invoices", "mail"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Since_ABaseline_CountsOnlyTheDelta(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Cardinality.Delta");
        var counter = meter.CreateCounter<long>("orders.placed");
        using var capture = InstrumentCapture.Of(counter);
        for (int i = 0; i < 5; i++)
            counter.Add(1, Tag("tenant", $"warmup-{i}"));

        var baseline = capture.Snapshot();
        counter.Add(1, Tag("tenant", "a"));
        counter.Add(1, Tag("tenant", "a"));

        await Assert.That(capture.Measurements.DistinctTagValues("tenant")).Count().IsEqualTo(6);
        await Assert.That(capture.Since(baseline).DistinctTagValues("tenant")).IsEquivalentTo(["a"]);
        await Assert.That(capture.Since(baseline).DistinctSeries()).IsEquivalentTo(["orders.placed {tenant=a}"]);
    }

    [Test]
    public async Task FlowScopedCapture_CountsOnlyItsOwnFlow_WhileOtherFlowsEmitOtherTenants(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Cardinality.Flow");
        var counter = meter.CreateCounter<long>("orders.placed");
        using var capture = InstrumentCapture.OfName<long>(meter, "orders.placed").InCurrentFlow();

        Task others;
        using (ExecutionContext.SuppressFlow())
        {
            others = Task.WhenAll(Enumerable.Range(0, 4).Select(flow => Task.Run(() =>
            {
                for (int i = 0; i < 25; i++)
                    counter.Add(1, Tag("tenant", $"other-{flow}-{i}"));
            }, ct)));
        }

        await Task.Run(() => counter.Add(1, Tag("tenant", "mine")), ct);
        counter.Add(1, Tag("tenant", "mine"));
        await others;

        await Assert.That(capture.Measurements.DistinctTagValues("tenant")).IsEquivalentTo(["mine"]);
        await Assert.That(capture.Measurements.DistinctSeries()).IsEquivalentTo(["orders.placed {tenant=mine}"]);
        await Assert.That(capture.Measurements.Describe()).Contains("100 measurement(s) of the captured instruments were emitted by other flows");
    }

    [Test]
    public async Task ReadWhileAnEmitterRuns_EachSnapshotIsConsistent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Cardinality.Live");
        var counter = meter.CreateCounter<long>("orders.placed");
        using var capture = InstrumentCapture.Of(counter);
        var emitter = Task.Run(() =>
        {
            for (int i = 0; i < 20_000; i++)
                counter.Add(1, Tag("tenant", $"t{i % 7}"), Tag("region", i % 2 == 0 ? "eu" : "us"));
        }, ct);

        var previous = 0;
        var reads = 0;
        while (!emitter.IsCompleted || reads is 0)
        {
            MeasurementSet set = capture.Measurements;
            var series = set.DistinctSeries().Count;
            await Assert.That(set.DistinctTagValues("tenant").Count).IsLessThanOrEqualTo(7);
            await Assert.That(series).IsLessThanOrEqualTo(14);
            await Assert.That(series).IsGreaterThanOrEqualTo(previous);
            previous = series;
            reads++;
        }

        await emitter;
        await Assert.That(capture.Measurements.DistinctSeries()).Count().IsEqualTo(14);
    }

    [Test]
    public async Task TenThousandDistinctIds_AreAllCounted(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Cardinality.Many");
        var counter = meter.CreateCounter<long>("orders.placed");
        using var capture = InstrumentCapture.Of(counter);
        for (int i = 0; i < 10_000; i++)
            counter.Add(1, Tag("order.id", i), Tag("tenant", "a"));

        await Assert.That(capture.Measurements.DistinctTagValues("order.id")).Count().IsEqualTo(10_000);
        await Assert.That(capture.Measurements.DistinctTagValues("tenant")).Count().IsEqualTo(1);
        await Assert.That(capture.Measurements.DistinctSeries()).Count().IsEqualTo(10_000);
    }
}
