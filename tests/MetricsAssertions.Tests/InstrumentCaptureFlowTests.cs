using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Tests for <see cref="InstrumentCapture.InCurrentFlow"/>: the single-instrument capture scoped to the async flow of the
/// code that calls it, for every way an <see cref="InstrumentCapture"/> is created.
/// </summary>
[Category("Smoke")]
[Timeout(30_000)]
internal sealed class InstrumentCaptureFlowTests
{
    private const string MeterName = "MetricsAssertions.Tests.InstrumentFlow";

    private static readonly Meter StaticMeter = new(MeterName);
    private static readonly Counter<long> StaticCounter = StaticMeter.CreateCounter<long>("instrument.flow.hits");

    private static void EmitFromASuppressedFlow(Action emit)
    {
        using (ExecutionContext.SuppressFlow())
        {
            var thread = new Thread(() => emit());
            thread.Start();
            thread.Join();
        }
    }

    [Test]
    public async Task Of_InCurrentFlow_RecordsTheFlow_AndIgnoresOtherFlows(CancellationToken ct)
    {
        using var capture = InstrumentCapture.Of(StaticCounter).InCurrentFlow();

        EmitFromASuppressedFlow(() => StaticCounter.Add(7));
        await Task.Run(() => StaticCounter.Add(2), ct);
        StaticCounter.Add(1);

        await Assert.That(capture.Total).IsEqualTo(3L);
    }

    [Test]
    public async Task EmptyFlowCapture_SaysThatOtherFlowsEmitted(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".Empty");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = InstrumentCapture.Of(counter).InCurrentFlow();

        EmitFromASuppressedFlow(() => counter.Add(4));

        await Assert.That(capture.Measurements.Describe())
            .IsEqualTo("    (no measurements captured; 1 measurement(s) of the captured instruments were emitted by other flows and ignored, because this capture records only the flow that created it)");
    }

    [Test]
    public async Task NonEmptyFlowCapture_SaysHowManyMeasurementsOfOtherFlowsItIgnored(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".NonEmpty");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = InstrumentCapture.Of(counter).InCurrentFlow();

        counter.Add(1);
        EmitFromASuppressedFlow(() => counter.Add(4));
        EmitFromASuppressedFlow(() => counter.Add(5));

        var described = capture.Measurements.Describe();
        await Assert.That(described).StartsWith("    hits = 1 @ ");
        await Assert.That(described).EndsWith("\n    (2 measurement(s) of the captured instruments were emitted by other flows and ignored, because this capture records only the flow that created it)");
    }

    [Test]
    public async Task FlowCapture_WithoutOtherFlows_HasNoNote(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".NoOtherFlows");
        var counter = meter.CreateCounter<long>("hits");
        using var capture = InstrumentCapture.Of(counter).InCurrentFlow();

        counter.Add(1);

        await Assert.That(capture.Measurements.Describe()).DoesNotContain("other flows");
    }

    [Test]
    public async Task OfObservable_InCurrentFlow_RecordsAPullInTheFlow(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".Observable");
        var gauge = meter.CreateObservableGauge("level", () => 42L);
        using var capture = InstrumentCapture.OfObservable(gauge).InCurrentFlow();

        capture.RecordObservable();

        await Assert.That(capture.LastValue).IsEqualTo(42d);
    }

    [Test]
    public async Task OfName_InCurrentFlow_CapturesEveryMatchingInstrument_ByMeterName(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        const string name = MeterName + ".ByName";
        using var capture = InstrumentCapture.OfName<long>(name, "hits").InCurrentFlow();
        using var first = new Meter(name);
        using var second = new Meter(name);
        using var otherName = new Meter(name + ".Other");

        first.CreateCounter<long>("hits").Add(1);
        second.CreateCounter<long>("hits").Add(10);
        first.CreateCounter<long>("misses").Add(100);
        otherName.CreateCounter<long>("hits").Add(1000);

        await Assert.That(capture.Total).IsEqualTo(11L);
    }

    [Test]
    public async Task OfName_InCurrentFlow_MatchesTheMeterScope(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        const string name = MeterName + ".Scoped";
        var scope = new object();
        using var capture = InstrumentCapture.OfName<long>(scope, name, "hits").InCurrentFlow();
        using var scoped = new Meter(name, version: null, tags: null, scope: scope);
        using var unscoped = new Meter(name);

        scoped.CreateCounter<long>("hits").Add(1);
        unscoped.CreateCounter<long>("hits").Add(10);

        await Assert.That(capture.Total).IsEqualTo(1L);
    }

    [Test]
    public async Task OfName_ByMeterInstance_InCurrentFlow_IgnoresAnotherMeterOfTheSameName(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        const string name = MeterName + ".Instance";
        using var meter = new Meter(name);
        using var twin = new Meter(name);
        using var capture = InstrumentCapture.OfName<long>(meter, "hits").InCurrentFlow();

        meter.CreateCounter<long>("hits").Add(1);
        twin.CreateCounter<long>("hits").Add(10);

        await Assert.That(capture.Total).IsEqualTo(1L);
    }

    [Test]
    public async Task InCurrentFlow_DisposesTheOriginalCapture(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".Original");
        var counter = meter.CreateCounter<long>("hits");
        var original = InstrumentCapture.Of(counter);
        using var scoped = original.InCurrentFlow();

        counter.Add(5);

        await Assert.That(scoped.Total).IsEqualTo(5L);
        await Assert.That(() => original.Count).Throws<ObjectDisposedException>();
        await Assert.That(() => original.InCurrentFlow()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task InCurrentFlow_RejectsAScopedCapture(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".Rejects");
        var counter = meter.CreateCounter<long>("hits");
        using var scoped = InstrumentCapture.Of(counter).InCurrentFlow();

        await Assert.That(() => scoped.InCurrentFlow()).Throws<InvalidOperationException>()
            .WithMessageContaining("already scoped to the current flow", StringComparison.Ordinal);
    }

    [Test]
    public async Task EmissionsBeforeInCurrentFlow_DoNotFailIt_AndAreNotCarriedOver(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".BeforeScope");
        var counter = meter.CreateCounter<long>("hits");
        var plain = InstrumentCapture.Of(counter);

        // Between creating the capture and scoping it, a parallel test emits, and so does this flow.
        EmitFromASuppressedFlow(() => counter.Add(100));
        counter.Add(10);
        using var scoped = plain.InCurrentFlow();
        counter.Add(1);

        await Assert.That(scoped.Total).IsEqualTo(1L);
    }

    [Test]
    public async Task OfName_InCurrentFlow_IgnoresASameNamedInstrumentOfAnotherValueType(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".ValueType");
        var counter = meter.CreateCounter<long>("hits");
        meter.CreateObservableGauge("hits", static double () => throw new InvalidOperationException("a double instrument must not be observed"));
        using var capture = InstrumentCapture.OfName<long>(meter, "hits").InCurrentFlow();

        counter.Add(3);
        capture.RecordObservable();

        await Assert.That(capture.Total).IsEqualTo(3L);
    }

    [Test]
    public async Task TwoParallelFlows_OnOneStaticCounter_EachSeesItsOwn(CancellationToken ct)
    {
        using var gate = new Barrier(2);

        InstrumentCapture Flow(string name)
        {
            var capture = InstrumentCapture.OfName<long>(MeterName, "instrument.flow.hits").InCurrentFlow();
            gate.SignalAndWait(ct);
            for (int i = 0; i < 500; i++)
                StaticCounter.Add(1, new KeyValuePair<string, object?>("flow", name));
            return capture;
        }

        var a = Task.Run(() => Flow("a"), ct);
        var b = Task.Run(() => Flow("b"), ct);
        using var captureA = await a;
        using var captureB = await b;

        await Assert.That(captureA.Tagged("flow", "a").Count).IsEqualTo(500);
        await Assert.That(captureA.Tagged("flow", "b").Count).IsEqualTo(0);
        await Assert.That(captureB.Tagged("flow", "b").Count).IsEqualTo(500);
        await Assert.That(captureB.Tagged("flow", "a").Count).IsEqualTo(0);
    }
}
