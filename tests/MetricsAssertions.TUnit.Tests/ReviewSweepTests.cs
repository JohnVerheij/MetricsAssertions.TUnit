using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>
/// Pins behavior found in a review sweep: a failure message that derives its count and its listing from one snapshot while
/// measurements keep arriving, a caller-supplied instrument name written on one line, empty sets that must not pass a
/// range or an average, and the tolerance comparisons for NaN and infinity.
/// </summary>
[Category("Smoke")]
[Timeout(30_000)]
internal sealed class ReviewSweepTests
{
    private const int Emissions = 20_000;
    private const string Prefix = "    orders = ";

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

    /// <summary>Returns the values of the dump lines of the <c>orders</c> instrument, in dump order.</summary>
    private static List<long> ListedValues(string message)
    {
        var values = new List<long>();
        foreach (var line in message.Split('\n'))
        {
            if (!line.StartsWith(Prefix, StringComparison.Ordinal))
                continue;
            var end = line.IndexOf(" @ ", StringComparison.Ordinal);
            values.Add(long.Parse(line[Prefix.Length..end], CultureInfo.InvariantCulture));
        }

        return values;
    }

    /// <summary>Returns the number after <paramref name="marker"/> in the message.</summary>
    private static long NumberAfter(string message, string marker)
    {
        var start = message.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = start;
        while (end < message.Length && (char.IsDigit(message[end]) || message[end] is '-'))
            end++;
        return long.Parse(message[start..end], CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Runs <paramref name="assertion"/> again and again while another thread keeps emitting to <paramref name="counter"/>,
    /// and counts the failure messages that <paramref name="consistent"/> rejects.
    /// </summary>
    private static async Task AssertConsistentWhileEmittingAsync(Counter<long> counter, Func<Task> assertion, Func<string, bool> consistent)
    {
        using var started = new ManualResetEventSlim();
        var done = 0;
        var emitter = new Thread(() =>
        {
            for (var i = 0; i < Emissions; i++)
            {
                counter.Add(i + 1);
                started.Set();
                Thread.Yield();
            }

            Volatile.Write(ref done, 1);
        });
        emitter.Start();
        try
        {
            started.Wait();
            var inconsistent = new List<string>();
            do
            {
                var message = await MessageAsync(assertion);
                if (!consistent(message))
                    inconsistent.Add(message.Length > 300 ? message[..300] : message);
            }
            while (Volatile.Read(ref done) == 0);

            await Assert.That(inconsistent).IsEmpty();
        }
        finally
        {
            emitter.Join();
        }
    }

    [Test]
    public async Task InstrumentCapture_HasMeasurementCount_CountMatchesListing_WhileEmitting(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Sweep.IcCount");
        var counter = meter.CreateCounter<long>("orders");
        using var capture = InstrumentCapture.Of(counter);

        await AssertConsistentWhileEmittingAsync(
            counter,
            async () => await Assert.That(capture).HasMeasurementCount(-1),
            message => NumberAfter(message, "it had ") == ListedValues(message).Count);
    }

    [Test]
    public async Task InstrumentCapture_HasNoMeasurements_CountMatchesListing_WhileEmitting(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Sweep.IcNone");
        var counter = meter.CreateCounter<long>("orders");
        using var capture = InstrumentCapture.Of(counter);

        await AssertConsistentWhileEmittingAsync(
            counter,
            async () => await Assert.That(capture).HasNoMeasurements(),
            message => message.Contains("(the assertion passed)", StringComparison.Ordinal)
                || NumberAfter(message, "it had ") == ListedValues(message).Count);
    }

    [Test]
    public async Task InstrumentCapture_HasLastValue_ValueMatchesListing_WhileEmitting(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Sweep.IcLast");
        var counter = meter.CreateCounter<long>("orders");
        using var capture = InstrumentCapture.Of(counter);

        bool LastMatches(string message)
        {
            var listed = ListedValues(message);
            return listed.Count is 0
                ? message.Contains("it had no measurements", StringComparison.Ordinal)
                : NumberAfter(message, "it was ") == listed[^1];
        }

        await AssertConsistentWhileEmittingAsync(counter, async () => await Assert.That(capture).HasLastValue(-1), LastMatches);
        await AssertConsistentWhileEmittingAsync(counter, async () => await Assert.That(capture).HasLastValue(-1, 0.5), LastMatches);
    }

    [Test]
    public async Task MeterCapture_HasMeasurementCount_CountMatchesListing_WhileEmitting(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Sweep.McCount");
        var counter = meter.CreateCounter<long>("orders");
        using var capture = MeterCapture.For(meter).Add<long>("orders");

        await AssertConsistentWhileEmittingAsync(
            counter,
            async () => await Assert.That(capture).HasMeasurementCount("orders", -1),
            message => NumberAfter(message, "it had ") == ListedValues(message).Count);
    }

    [Test]
    public async Task MeterCapture_HasUpDownCounterValue_ValueMatchesListing_WhileEmitting(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Sweep.McUpDown");
        var counter = meter.CreateCounter<long>("orders");
        using var capture = MeterCapture.For(meter).Add<long>("orders");

        await AssertConsistentWhileEmittingAsync(
            counter,
            async () => await Assert.That(capture).HasUpDownCounterValue("orders", -1),
            message => NumberAfter(message, "it was ") == ListedValues(message).Sum());
    }

    [Test]
    public async Task MeterCapture_NotBundledAndNegativeDelta_WriteTheInstrumentNameOnOneLine(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Sweep.Names");
        var counter = meter.CreateUpDownCounter<long>("a\r\nb");
        using var capture = MeterCapture.For(meter).Add<long>("a\r\nb");
        counter.Add(-1);

        var notBundled = await MessageAsync(async () => await Assert.That(capture).HasMeasurementCount("x\ny", 1));
        var negative = await MessageAsync(async () => await Assert.That(capture).HasCounterTotal("a\r\nb", 0));

        await Assert.That(notBundled).Contains("but no instrument named x\\ny is bundled");
        await Assert.That(negative).Contains("but a\\r\\nb recorded a negative delta");
        await Assert.That(negative).Contains("    a\\r\\nb = -1 @ ");
    }

    [Test]
    public async Task HasAllSamplesInRange_FailsOnAnEmptySet(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var message = await MessageAsync(async () => await Assert.That(MeasurementSet.Empty).HasAllSamplesInRange(0, 100));

        await Assert.That(message).Contains("it had no samples");
    }

    [Test]
    public async Task HasSampleAverage_FailsOnAnEmptySet(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var message = await MessageAsync(async () => await Assert.That(MeasurementSet.Empty).HasSampleAverage(0));

        await Assert.That(message).Contains("it had no samples");
    }

    [Test]
    public async Task ToleranceAssertions_HandleNaNAndInfinity(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.Sweep.Special");
        var gauge = meter.CreateHistogram<double>("computed");
        using var capture = InstrumentCapture.Of(gauge);
        gauge.Record(double.PositiveInfinity);
        MeasurementSet infinite = capture.Measurements;

        await Assert.That(infinite).HasSampleSum(double.PositiveInfinity);
        await Assert.That(infinite).HasSampleAverage(double.PositiveInfinity);
        await Assert.That(capture).HasLastValue(double.PositiveInfinity, 1d);
        await Assert.That(infinite).HasSamples([double.PositiveInfinity], 1d);
        await Assert.That(async () => await Assert.That(infinite).HasSampleSum(double.NegativeInfinity)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(infinite).HasSampleSum(1d, 1d)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasLastValue(double.NegativeInfinity, 1d)).Throws<AssertionException>();

        using var negativeCapture = InstrumentCapture.Of(gauge);
        gauge.Record(double.NegativeInfinity);
        await Assert.That(negativeCapture).HasLastValue(double.NegativeInfinity, 1d);
        await Assert.That(async () => await Assert.That(negativeCapture).HasLastValue(double.PositiveInfinity, 1d)).Throws<AssertionException>();

        gauge.Record(double.NaN);
        MeasurementSet notANumber = capture.Measurements;

        await Assert.That(async () => await Assert.That(notANumber).HasSampleSum(double.NaN)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(notANumber).HasSampleAverage(double.NaN)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasLastValue(double.NaN, 1d)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasLastValue(1d, 1d)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(notANumber).HasSamples([double.PositiveInfinity, double.NaN], 1d)).Throws<AssertionException>();
    }
}
