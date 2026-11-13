using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Real-use pass for the core: how the captures behave where a test suite really calls them, next to other tests, other
/// threads and unusual values. Each test pins what was measured, so a later change shows up as a failing test.
/// </summary>
[Category("Smoke")]
[Timeout(30_000)]
internal sealed class RealUseTests
{
    private const string SharedMeterName = "MetricsAssertions.Tests.RealUse.Shared";

    private static readonly Meter SharedMeter = new(SharedMeterName);
    private static readonly Counter<long> SharedCounter = SharedMeter.CreateCounter<long>("real.hits");

    private enum Color
    {
        Red,
        Green,
    }

    private static void EmitFromAnUnrelatedThread(long amount)
    {
        using var suppressed = ExecutionContext.SuppressFlow();
        var thread = new Thread(() => SharedCounter.Add(amount));
        thread.Start();
        thread.Join();
    }

    [Test]
    public async Task CaptureByName_SeesAnotherFlowsMeasurements_OnAStaticMeter(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var byName = MeterCapture.For(SharedMeterName).Add<long>("real.hits");

        // Another test of the process: its own flow, no relation to this one. The static meter is the same.
        EmitFromAnUnrelatedThread(7);

        // The capture by name counts it: a keyed [NotInParallel] on this test would not have kept it out, because the
        // other emitter is any code of the process.
        await Assert.That(byName.CounterTotal("real.hits")).IsGreaterThanOrEqualTo(7L);
    }

    [Test]
    public async Task TwoCapturesOfOneInstrument_BothRecord_AndDisposeOrderDoesNotMatter(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.Two");
        var counter = meter.CreateCounter<long>("hits");
        var first = InstrumentCapture.Of(counter);
        var second = InstrumentCapture.Of(counter);
        counter.Add(1);

        var totalOfFirst = first.Total;
        first.Dispose();
        counter.Add(2);
        var totalOfSecond = second.Total;
        second.Dispose();
        counter.Add(4);

        await Assert.That(totalOfFirst).IsEqualTo(1L);
        await Assert.That(totalOfSecond).IsEqualTo(3L);
        await Assert.That(() => first.Total).Throws<ObjectDisposedException>();
        await Assert.That(() => second.Count).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task AfterDispose_EveryReadThrowsObjectDisposed_ForBothCaptureKinds(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.AfterDispose");
        var counter = meter.CreateCounter<long>("hits");
        _ = meter.CreateObservableGauge("gauge", () => 1);

        foreach (var flowScoped in new[] { false, true })
        {
            var capture = flowScoped
                ? MeterCapture.For(meter).InCurrentFlow().Add<long>("hits")
                : MeterCapture.For(meter).Add<long>("hits");
            counter.Add(1);
            capture.Dispose();

            await Assert.That(() => capture.Measurements).Throws<ObjectDisposedException>();
            await Assert.That(() => capture["hits"].Count).Throws<ObjectDisposedException>();
            await Assert.That(() => capture.RecordObservable()).Throws<ObjectDisposedException>();
            await Assert.That(async () => await capture["hits"].WaitForAsync(5, ct)).Throws<ObjectDisposedException>();
            await Assert.That(() => capture.Dispose()).ThrowsNothing();
        }
    }

    [Test]
    public async Task AddingAnInstrumentTwice_ReplacesTheCapture_AndStartsAFreshStream(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.Replace");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = MeterCapture.For(meter).Add<long>("hits");
        counter.Add(5);

        capture.Add<long>("hits");
        counter.Add(2);

        // The first capture was disposed with what it held; the replacement counts from its creation.
        await Assert.That(capture.CounterTotal("hits")).IsEqualTo(2L);
    }

    [Test]
    public async Task Measurements_ReadWhileAnotherThreadEmits_AreConsistentSnapshots(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.Concurrent");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = MeterCapture.For(meter).Add<long>("hits");
        var emitter = Task.Run(() =>
        {
            for (int i = 0; i < 20_000; i++)
                counter.Add(1);
        }, ct);

        var previous = 0;
        while (!emitter.IsCompleted)
        {
            var count = capture.Measurements.Count;
            var samples = capture.Samples("hits").Count;
            await Assert.That(count).IsGreaterThanOrEqualTo(previous);
            await Assert.That(samples).IsGreaterThanOrEqualTo(count);
            previous = count;
        }

        await emitter;
        await Assert.That(capture.MeasurementCount("hits")).IsEqualTo(20_000);
    }

    [Test]
    public async Task DisposeWhileAnotherThreadEmits_DoesNotThrow(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.DisposeRace");
        var counter = meter.CreateCounter<long>("hits");
        for (int round = 0; round < 50; round++)
        {
            var capture = MeterCapture.For(meter).Add<long>("hits");
            using var stop = new CancellationTokenSource();
            var emitter = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                    counter.Add(1);
            }, ct);

            capture.Dispose();
            await stop.CancelAsync();
            await emitter;
        }

        await Assert.That(counter.Enabled).IsFalse();
    }

    [Test]
    public async Task ForMeterAfterTheMeterIsDisposed_CapturesNothing_AndDoesNotThrow(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var meter = new Meter("MetricsAssertions.Tests.RealUse.Disposed");
        var counter = meter.CreateCounter<long>("hits");
        meter.Dispose();

        using var capture = MeterCapture.For(meter).Add<long>("hits");
        counter.Add(3);

        await Assert.That(capture.MeasurementCount("hits")).IsEqualTo(0);
    }

    [Test]
    public async Task WaitForAsync_WithACountNeverReached_ThrowsWhenTheTokenIsCanceled(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.Wait");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = InstrumentCapture.Of(counter);
        counter.Add(1);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await capture.WaitForAsync(1, ct);
        OperationCanceledException? canceled = null;
        try
        {
            await capture.WaitForAsync(2, cts.Token);
        }
        catch (OperationCanceledException exception)
        {
            canceled = exception;
        }

        await Assert.That(canceled).IsNotNull();
    }

    [Test]
    public async Task TagValuesOfOtherTypes_AreComparedAsInvariantText(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.Tags");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = InstrumentCapture.Of(counter);
        counter.Add(1, new KeyValuePair<string, object?>("ok", true), new KeyValuePair<string, object?>("color", Color.Green));
        counter.Add(1, new KeyValuePair<string, object?>("none", null));
        counter.Add(1, new KeyValuePair<string, object?>("none", string.Empty));

        await Assert.That(capture.HasMeasurementTagged("ok", true)).IsTrue();
        await Assert.That(capture.HasMeasurementTagged("ok", "True")).IsTrue();
        await Assert.That(capture.HasMeasurementTagged("ok", "true")).IsFalse();
        await Assert.That(capture.HasMeasurementTagged("color", Color.Green)).IsTrue();
        await Assert.That(capture.HasMeasurementTagged("color", "Green")).IsTrue();

        // A null tag value and an empty string are the same text, so a query for one matches both.
        await Assert.That(capture.Tagged("none", null).Count).IsEqualTo(2);
        await Assert.That(capture.Tagged("none", string.Empty).Count).IsEqualTo(2);
        await Assert.That(capture.Tagged("missing", null).Count).IsEqualTo(0);
    }

    [Test]
    public async Task ValuesOfEveryInstrumentType_ProjectToDouble(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.Types");
        var money = meter.CreateHistogram<decimal>("money");
        var floats = meter.CreateHistogram<float>("floats");
        var bytes = meter.CreateHistogram<byte>("bytes");
        var shorts = meter.CreateHistogram<short>("shorts");
        using var capture = MeterCapture.For(meter)
            .Add<decimal>("money").Add<float>("floats").Add<byte>("bytes").Add<short>("shorts");

        money.Record(12.34m);
        floats.Record(1.5f);
        bytes.Record(255);
        shorts.Record(-3);

        await Assert.That(capture.Samples("money")[0]).IsEqualTo(12.34d);
        await Assert.That(capture.Samples("floats")[0]).IsEqualTo(1.5d);
        await Assert.That(capture.Samples("bytes")[0]).IsEqualTo(255d);
        await Assert.That(capture.Samples("shorts")[0]).IsEqualTo(-3d);
    }

    [Test]
    public async Task ExtremeValues_NaNInfinityAndLongMaxValue_AreProjectedAsIs(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.Extremes");
        var histogram = meter.CreateHistogram<double>("h");
        var counter = meter.CreateCounter<long>("c");
        using var capture = MeterCapture.For(meter).Add<double>("h").Add<long>("c");

        histogram.Record(double.NaN);
        histogram.Record(double.PositiveInfinity);
        counter.Add(long.MaxValue);

        await Assert.That(double.IsNaN(capture.Samples("h")[0])).IsTrue();
        await Assert.That(double.IsPositiveInfinity(capture.Samples("h")[1])).IsTrue();
        await Assert.That(capture.Measurements.ForInstrument("c").Sum).IsEqualTo(9.2233720368547758E+18d);

        // Total is the sum rounded to a long: a sum that no long can hold throws instead of wrapping.
        await Assert.That(() => capture.CounterTotal("c")).Throws<OverflowException>();
        await Assert.That(() => capture.Measurements.ForInstrument("h").Total).Throws<OverflowException>();
    }

    [Test]
    public async Task AnInstrumentThatNeverExisted_ReadsAsNothingEmitted(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = MeterCapture.For("MetricsAssertions.Tests.RealUse.NoSuchMeter").Add<long>("typo.name");

        // A name typo and a meter that emitted nothing look the same: no instrument is attached, nothing is recorded.
        await Assert.That(capture.CounterTotal("typo.name")).IsEqualTo(0L);
        await Assert.That(capture.MeasurementCount("typo.name")).IsEqualTo(0);
        await Assert.That(MeterInspector.IsPublished("MetricsAssertions.Tests.RealUse.NoSuchMeter", "typo.name")).IsFalse();
    }

    [Test]
    public async Task ManyInstrumentsRecordedInParallel_AreAllCounted(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.RealUse.Parallel");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = MeterCapture.For(meter).Add<long>("hits");

        await Parallel.ForAsync(0, 8, ct, (_, _) =>
        {
            for (int i = 0; i < 1000; i++)
                counter.Add(1);
            return ValueTask.CompletedTask;
        });

        await Assert.That(capture.CounterTotal("hits")).IsEqualTo(8000L);
        await Assert.That(capture.Measurements.All.Select(m => m.Value).Distinct().Count()).IsEqualTo(1);
    }
}
