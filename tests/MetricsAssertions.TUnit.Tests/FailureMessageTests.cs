using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>
/// Pins the full failure text of every assertion: TUnit's "Expected ..." line from the assertion's expectation message,
/// the "but ..." line that describes only the actual value, and the dump of the captured measurements. Timestamps come from
/// a fixed clock. TUnit renders the placeholders of an expectation message with the current culture, so the tests run under the invariant culture.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class FailureMessageTests
{
    private const string Stamp = "2026-11-13T12:00:00.0000000+00:00";

    private static readonly TimeProvider Clock = new FixedTimeProvider(FixedTimeProvider.Instant);

    private static readonly double[] OffByOne = [1.5, 3d];
    private static readonly double[] Unordered = [3d, 1.5];

    /// <summary>Runs <paramref name="body"/> with the invariant culture, restoring the caller's culture afterwards.</summary>
    private static async Task InvariantAsync(Func<Task> body)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            await body();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>Returns the failure message of <paramref name="assertion"/> without TUnit's trailing call-site line.</summary>
    private static async Task<string> MessageAsync(Func<Task> assertion)
    {
        try
        {
            await assertion();
        }
        catch (AssertionException exception)
        {
            var text = exception.Message.ReplaceLineEndings("\n");
            var callSite = text.IndexOf("\n\nat ", StringComparison.Ordinal);
            return callSite < 0 ? text : text[..callSite];
        }

        return "(the assertion passed)";
    }

    [Test]
    public async Task InstrumentCapture_FailureTexts(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await InvariantAsync(async () =>
        {
            using var meter = new Meter("MetricsAssertions.TUnit.Tests.Messages.IC");
            var counter = meter.CreateCounter<long>("orders.placed");
            var upDown = meter.CreateUpDownCounter<long>("queue.depth");
            using var capture = InstrumentCapture.Of(counter, Clock);
            using var depth = InstrumentCapture.Of(upDown, Clock);
            using var empty = InstrumentCapture.Of(meter.CreateCounter<long>("orders.none"), Clock);
            counter.Add(3, new KeyValuePair<string, object?>("route", "/orders"));
            upDown.Add(-2);
            const string Dump = $"\n  captured:\n    orders.placed = 3 {{route=/orders}} @ {Stamp}";

            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasCounterTotal(5)))
                .IsEqualTo("Expected to have counter total 5\nbut it was 3" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(depth).HasCounterTotal(5)))
                .IsEqualTo($"Expected to have counter total 5\nbut the capture recorded a negative delta, which a counter never does\n  captured:\n    queue.depth = -2 @ {Stamp}");
            await Assert.That(await MessageAsync(async () => await Assert.That(depth).HasCounterTotalAtLeast(1)))
                .IsEqualTo($"Expected to have counter total of at least 1\nbut the capture recorded a negative delta, which a counter never does\n  captured:\n    queue.depth = -2 @ {Stamp}");
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasCounterTotalAtLeast(5)))
                .IsEqualTo("Expected to have counter total of at least 5\nbut it was 3" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasUpDownCounterValue(5)))
                .IsEqualTo("Expected to have up-down counter value 5\nbut it was 3" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasMeasurementCount(5)))
                .IsEqualTo("Expected to have 5 measurement(s)\nbut it had 1" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasNoMeasurements()))
                .IsEqualTo("Expected to have no measurements\nbut it had 1" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasLastValue(5.5)))
                .IsEqualTo("Expected to have last value 5.5\nbut it was 3" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasLastValue(5.5, 0.25)))
                .IsEqualTo("Expected to have last value 5.5 (within 0.25)\nbut it was 3" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(empty).HasLastValue(5.5)))
                .IsEqualTo("Expected to have last value 5.5\nbut it had no measurements\n  captured:\n    (no measurements captured)");
            await Assert.That(await MessageAsync(async () => await Assert.That(empty).HasLastValue(5.5, 0.25)))
                .IsEqualTo("Expected to have last value 5.5 (within 0.25)\nbut it had no measurements\n  captured:\n    (no measurements captured)");
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasTaggedMeasurement("route", "/missing")))
                .IsEqualTo("Expected to have a measurement tagged route=/missing\nbut none matched" + Dump);
        });
    }

    [Test]
    public async Task MeasurementSet_FailureTexts(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await InvariantAsync(async () =>
        {
            using var meter = new Meter("MetricsAssertions.TUnit.Tests.Messages.MS");
            var histogram = meter.CreateHistogram<double>("latency");
            using var capture = InstrumentCapture.Of(histogram, Clock);
            histogram.Record(1.5);
            histogram.Record(2.5);
            var set = capture.Measurements;
            const string Dump = $"\n  captured:\n    latency = 1.5 @ {Stamp}\n    latency = 2.5 @ {Stamp}";

            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasCounterTotal(9)))
                .IsEqualTo("Expected to have counter total 9\nbut it was 4" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasCounterTotalAtLeast(9)))
                .IsEqualTo("Expected to have counter total of at least 9\nbut it was 4" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasMeasurementCount(9)))
                .IsEqualTo("Expected to have 9 measurement(s)\nbut it had 2" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasNoMeasurements()))
                .IsEqualTo("Expected to have no measurements\nbut it had 2" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasSampleSum(9.5, 0.5)))
                .IsEqualTo("Expected to have a sample sum of 9.5 (within 0.5)\nbut it was 4" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasSampleAverage(9.5, 0.5)))
                .IsEqualTo("Expected to have a sample average of 9.5 (within 0.5)\nbut it was 2" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasAllSamplesInRange(0.5, 2)))
                .IsEqualTo("Expected to have all samples within [0.5, 2]\nbut the samples were [1.5, 2.5]" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasSamples(1.5, 3)))
                .IsEqualTo("Expected to have the expected samples in order\nbut the samples were [1.5, 2.5], expected [1.5, 3]" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasSamples(OffByOne, 0.25)))
                .IsEqualTo("Expected to have the expected samples in order, each within 0.25\nbut the samples were [1.5, 2.5], expected [1.5, 3]" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasSamplesInAnyOrder(3, 1.5)))
                .IsEqualTo("Expected to have the expected samples in any order\nbut the samples were [1.5, 2.5], expected [3, 1.5]" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasSamplesInAnyOrder(Unordered, 0.25)))
                .IsEqualTo("Expected to have the expected samples in any order, each within 0.25\nbut the samples were [1.5, 2.5], expected [3, 1.5]" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasEveryMeasurementTagged("route")))
                .IsEqualTo("Expected to have every measurement tagged with route\nbut not all were" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(set).HasTaggedMeasurement("route", "/x")))
                .IsEqualTo("Expected to have a measurement tagged route=/x\nbut none matched" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(MeasurementSet.Empty).HasCounterTotal(1)))
                .IsEqualTo("Expected to have counter total 1\nbut it was 0\n  captured:\n    (no measurements captured)");
        });
    }

    [Test]
    public async Task MeterCapture_FailureTexts(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await InvariantAsync(async () =>
        {
            using var meter = new Meter("MetricsAssertions.TUnit.Tests.Messages.MC");
            var counter = meter.CreateCounter<long>("orders.placed");
            var upDown = meter.CreateUpDownCounter<long>("queue.depth");
            using var capture = MeterCapture.For(meter, Clock).Add<long>("orders.placed").Add<long>("queue.depth");
            counter.Add(3, new KeyValuePair<string, object?>("route", "/orders"));
            upDown.Add(-2);
            const string Dump = $"\n  captured:\n    orders.placed = 3 {{route=/orders}} @ {Stamp}\n    queue.depth = -2 @ {Stamp}";

            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasCounterTotal("orders.placed", 5)))
                .IsEqualTo($"Expected to have counter orders.placed total 5\nbut it was 3\n  captured:\n    orders.placed = 3 {{route=/orders}} @ {Stamp}");
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasCounterTotal("queue.depth", 5)))
                .IsEqualTo($"Expected to have counter queue.depth total 5\nbut queue.depth recorded a negative delta, which a counter never does\n  captured:\n    queue.depth = -2 @ {Stamp}");
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasCounterTotal("missing", 5)))
                .IsEqualTo("Expected to have counter missing total 5\nbut no instrument named missing is bundled" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasUpDownCounterValue("queue.depth", 5)))
                .IsEqualTo("Expected to have up-down counter queue.depth value 5\nbut it was -2" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasUpDownCounterValue("missing", 5)))
                .IsEqualTo("Expected to have up-down counter missing value 5\nbut no instrument named missing is bundled" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasMeasurementCount("orders.placed", 5)))
                .IsEqualTo("Expected to have 5 measurement(s) for orders.placed\nbut it had 1" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasMeasurementCount("missing", 5)))
                .IsEqualTo("Expected to have 5 measurement(s) for missing\nbut no instrument named missing is bundled" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasTaggedMeasurement("orders.placed", "route", "/x")))
                .IsEqualTo("Expected to have a measurement for orders.placed tagged route=/x\nbut none matched" + Dump);
            await Assert.That(await MessageAsync(async () => await Assert.That(capture).HasTaggedMeasurement("missing", "route", "/x")))
                .IsEqualTo("Expected to have a measurement for missing tagged route=/x\nbut no instrument named missing is bundled" + Dump);
        });
    }
}
