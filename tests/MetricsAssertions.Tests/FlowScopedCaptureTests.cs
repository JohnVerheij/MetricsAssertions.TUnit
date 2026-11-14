using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Tests for <see cref="MeterCapture.InCurrentFlow"/>: a capture that records only the measurements emitted in the async
/// flow of the code that created it. Every test emits to the same static counter or to a meter of the same name, so
/// the tests running in parallel with each other are themselves the cross-flow traffic the capture has to ignore.
/// </summary>
[Category("Smoke")]
[Timeout(30_000)]
internal sealed class FlowScopedCaptureTests
{
    private const string MeterName = "MetricsAssertions.Tests.FlowScoped";

    private static readonly Meter StaticMeter = new(MeterName);
    private static readonly Counter<long> StaticCounter = StaticMeter.CreateCounter<long>("flow.hits");

    private static MeterCapture NewCapture(TimeProvider? clock = null)
        => MeterCapture.For(MeterName, clock).InCurrentFlow().Add<long>("flow.hits");

    private static void Emit(int count, string flow)
    {
        for (int i = 0; i < count; i++)
            StaticCounter.Add(1, new KeyValuePair<string, object?>("flow", flow));
    }

    [Test]
    public async Task TwoParallelFlows_OnOneStaticCounter_EachSeesExactlyItsOwn(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        for (int round = 0; round < 50; round++)
        {
            using var gate = new Barrier(2);

            async Task<MeterCapture> FlowAsync(string name)
            {
                var capture = NewCapture();
                gate.SignalAndWait(ct);
                for (int i = 0; i < 1000; i++)
                {
                    StaticCounter.Add(1, new KeyValuePair<string, object?>("flow", name));
                    if (i % 250 == 0)
                        await Task.Yield();
                }

                return capture;
            }

            var a = Task.Run(() => FlowAsync("a"), ct);
            var b = Task.Run(() => FlowAsync("b"), ct);
            using var captureA = await a;
            using var captureB = await b;

            // The captures were created inside the async delegates, whose flows end with them; the totals were read after the
            // emission, so both captures hold their flow's whole stream.
            await Assert.That(captureA.CounterTotal("flow.hits")).IsEqualTo(1000L);
            await Assert.That(captureB.CounterTotal("flow.hits")).IsEqualTo(1000L);
            await Assert.That(captureA.Measurements.All.All(m => Equals(m.Tags["flow"], "a"))).IsTrue();
            await Assert.That(captureB.Measurements.All.All(m => Equals(m.Tags["flow"], "b"))).IsTrue();
        }
    }

    [Test]
    public async Task EmissionsInTheFlow_AreCounted_AfterAwait_TaskRun_ParallelForEach_ThreadPool_AndTimer(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = NewCapture();

        await Task.Yield();
        Emit(1, "await");
        await Task.Run(() => Emit(1, "task-run"), ct);
        await Parallel.ForEachAsync(Enumerable.Range(0, 4), ct, (_, _) =>
        {
            Emit(1, "parallel");
            return ValueTask.CompletedTask;
        });

        using var pooled = new ManualResetEventSlim();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Emit(1, "pool");
            pooled.Set();
        });
        pooled.Wait(ct);

        using var ticked = new ManualResetEventSlim();
        using var timer = new Timer(_ =>
        {
            Emit(1, "timer");
            ticked.Set();
        }, null, 1, Timeout.Infinite);
        ticked.Wait(ct);

        await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(8L);
        var flows = capture.Measurements.All.Select(m => Convert.ToString(m.Tags["flow"], CultureInfo.InvariantCulture)!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        await Assert.That(string.Join(',', flows)).IsEqualTo("await,parallel,pool,task-run,timer");
    }

    [Test]
    public async Task EmissionsOutsideTheFlow_AreNotCounted_SuppressedFlowOrServiceStartedBefore(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var release = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        using var done = new CountdownEvent(1);

        // A background service that was already running before the capture exists.
        var service = new Thread(() =>
        {
            started.Set();
            try
            {
                release.Wait(ct);
            }
            catch (OperationCanceledException)
            {
                // The test was canceled: stop without emitting.
            }

            Emit(5, "service");
            done.Signal();
        });
        service.Start();
        try
        {
            started.Wait(ct);

            using var capture = NewCapture();

            // A thread started with the execution context suppressed does not carry the marker.
            using (ExecutionContext.SuppressFlow())
            {
                var suppressed = new Thread(() => Emit(7, "suppressed"));
                suppressed.Start();
                suppressed.Join();
            }

            release.Set();
            done.Wait(ct);
            Emit(2, "own");

            await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(2L);
            await Assert.That(capture.Measurements.IsEmpty).IsFalse();
        }
        finally
        {
            // On every exit path the service ends before its events are disposed: it signals done as its last step.
            release.Set();
            service.Join();
        }
    }

    [Test]
    public async Task ObservableGauge_RecordObservableInTheFlow_IsRecorded(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".Observable");
        _ = meter.CreateObservableGauge("flow.gauge", () => 41);
        using var capture = MeterCapture.For(meter.Name).InCurrentFlow().Add<int>("flow.gauge");

        await Task.Yield();
        capture.RecordObservable();
        capture.RecordObservable();

        await Assert.That(capture.MeasurementCount("flow.gauge")).IsEqualTo(2);
        await Assert.That(capture["flow.gauge"].LastValue).IsEqualTo(41d);
    }

    [Test]
    public async Task ObservableGauge_PulledByASiblingFlow_IsNotRecordedHere(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".Observable2");
        _ = meter.CreateObservableGauge("flow.gauge", () => 3);

        // Two sibling flows, both started before either capture exists: neither includes the other.
        using var barrier = new Barrier(2);

        Task<(MeterCapture Capture, int Count)> FlowAsync(bool pulls) => Task.Run(() =>
        {
            var capture = MeterCapture.For(meter.Name).InCurrentFlow().Add<int>("flow.gauge");
            barrier.SignalAndWait(ct);
            if (pulls)
                capture.RecordObservable();
            barrier.SignalAndWait(ct);
            return (capture, capture.MeasurementCount("flow.gauge"));
        }, ct);

        var puller = FlowAsync(pulls: true);
        var bystander = FlowAsync(pulls: false);
        var pulled = await puller;
        var untouched = await bystander;
        using var pullerCapture = pulled.Capture;
        using var bystanderCapture = untouched.Capture;

        await Assert.That(pulled.Count).IsEqualTo(1);
        await Assert.That(untouched.Count).IsEqualTo(0);
        // A pull reaches only the listener that pulled, so the bystander has nothing to ignore either.
        await Assert.That(bystanderCapture.Measurements.IsEmpty).IsTrue();
        await Assert.That(pullerCapture.Measurements.IsEmpty).IsFalse();
    }

    [Test]
    public async Task Timestamps_FollowTheCapturesClock(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var instant = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        using var capture = NewCapture(new FixedTimeProvider(instant));

        Emit(1, "clock");

        await Assert.That(capture["flow.hits"].Measurements.All[0].Timestamp).IsEqualTo(instant);
    }

    [Test]
    public async Task WaitForAsync_CompletesAcrossThreads_AndCancels(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = NewCapture();
        var instrument = capture["flow.hits"];

        var waiting = instrument.WaitForAsync(3, ct);
        await Assert.That(waiting.IsCompleted).IsFalse();
        await Task.Run(() => Emit(3, "wait"), ct);
        await waiting.WaitAsync(TimeSpan.FromSeconds(10), ct);

        await instrument.WaitForAsync(1, ct);

        using var cts = new CancellationTokenSource();
        var neverReached = instrument.WaitForAsync(100, cts.Token);
        await cts.CancelAsync();
        OperationCanceledException? canceled = null;
        try
        {
            await neverReached;
        }
        catch (OperationCanceledException exception)
        {
            canceled = exception;
        }

        await Assert.That(canceled).IsNotNull();

        await Assert.That(async () => await instrument.WaitForAsync(100, new CancellationToken(canceled: true)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Dispose_ReleasesTheListener(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".Dispose");
        var counter = meter.CreateCounter<long>("flow.hits");
        var capture = MeterCapture.For(meter.Name).InCurrentFlow().Add<long>("flow.hits");
        await Assert.That(counter.Enabled).IsTrue();

        capture.Dispose();
        counter.Add(1);

        await Assert.That(counter.Enabled).IsFalse();
    }

    [Test]
    public async Task EveryMatchingInstrument_IsCaptured_NotTheFirstOnly(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var first = new Meter(MeterName + ".Many");
        using var second = new Meter(MeterName + ".Many");
        var a = first.CreateCounter<long>("flow.hits");
        using var capture = MeterCapture.For(first.Name).InCurrentFlow().Add<long>("flow.hits");

        // The second meter (same name) is created after the capture: capture by name attaches to it too.
        var b = second.CreateCounter<long>("flow.hits");
        a.Add(1);
        b.Add(2);

        await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(3L);
    }

    [Test]
    public async Task FlowsStartedAfterTheCapture_AreIncluded(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = NewCapture();
        await Task.Yield();
        Emit(4, "own");
        await Task.Run(() => Emit(100, "child"), ct);

        await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(104L);
    }

    [Test]
    public async Task CreatedInsideAnAwaitedAsyncHelper_DoesNotReachTheCaller(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        static async Task<MeterCapture> CreateAsync()
        {
            await Task.Yield();
            return NewCapture();
        }

        using var capture = await CreateAsync();
        Emit(3, "caller");

        // The marker was set inside the helper's own flow and does not flow back to the caller: the documented limit.
        await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(0L);
        await Assert.That(capture.Measurements.Describe()).Contains("emitted by other flows and ignored");
    }

    [Test]
    public async Task TwoCapturesInOneFlow_BothSeeTheFlow_InEitherOrder(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var first = NewCapture();
        using var second = NewCapture();

        Emit(2, "both");

        await Assert.That(first.CounterTotal("flow.hits")).IsEqualTo(2L);
        await Assert.That(second.CounterTotal("flow.hits")).IsEqualTo(2L);
    }

    [Test]
    public async Task InstrumentAddedByReference_IsFlowScoped(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = MeterCapture.For(StaticMeter).InCurrentFlow().Add(StaticCounter);

        Emit(2, "ref");
        await Task.Run(() => Emit(1, "ref-child"), ct);

        await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(3L);
    }

    [Test]
    public async Task InstrumentCaptureOfAnotherMeterInstance_IsNotCaptured_WhenCapturedByInstance(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var other = new Meter(MeterName);
        var otherCounter = other.CreateCounter<long>("flow.hits");
        using var capture = MeterCapture.For(other).InCurrentFlow().Add<long>("flow.hits");

        Emit(5, "static");
        otherCounter.Add(2);

        await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(2L);
    }

    [Test]
    public async Task ScopedMeter_IsCaptured_OnlyWithTheMatchingScope(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var scope = new object();
        using var scoped = new Meter(new MeterOptions(MeterName + ".Scoped") { Scope = scope });
        using var unscoped = new Meter(MeterName + ".Scoped");
        var scopedCounter = scoped.CreateCounter<long>("flow.hits");
        var unscopedCounter = unscoped.CreateCounter<long>("flow.hits");
        using var capture = MeterCapture.For(MeterName + ".Scoped", scope).InCurrentFlow().Add<long>("flow.hits");

        scopedCounter.Add(2);
        unscopedCounter.Add(40);

        await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(2L);
    }

    [Test]
    public async Task Builder_RejectsALateOrRepeatedCall(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var late = MeterCapture.For(MeterName).Add<long>("flow.hits");
        await Assert.That(() => late.InCurrentFlow()).Throws<InvalidOperationException>();

        using var twice = MeterCapture.For(MeterName).InCurrentFlow();
        await Assert.That(() => twice.InCurrentFlow()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ReplacingAnInstrument_KeepsTheFlowScope(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = MeterCapture.For(MeterName).InCurrentFlow().Add<long>("flow.hits").Add<long>("flow.hits");

        Emit(2, "replace");
        await Task.Run(() => Emit(100, "other"), ct);

        await Assert.That(capture.CounterTotal("flow.hits")).IsEqualTo(102L);
    }
}
