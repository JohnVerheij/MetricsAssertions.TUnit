using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Where a flow-scoped capture can not be created in a TUnit test class: the flow marker set in the constructor, in a
/// <c>[Before(Test)]</c> hook or in a <c>[Before(Class)]</c> hook does not reach the test body (measured).
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class FlowScopedLifecycleTests : IDisposable
{
    private const string MeterName = "MetricsAssertions.Tests.FlowLifecycle";

    private static readonly Meter LifecycleMeter = new(MeterName);
    private static readonly Counter<long> Hits = LifecycleMeter.CreateCounter<long>("flow.hits");
    private static MeterCapture? s_beforeClass;

    private readonly MeterCapture _constructed = MeterCapture.For(MeterName).InCurrentFlow().Add<long>("flow.hits");
    private MeterCapture? _beforeTest;

    [Before(Class)]
    public static void CreateInBeforeClass(ClassHookContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        s_beforeClass = MeterCapture.For(MeterName).InCurrentFlow().Add<long>("flow.hits");
    }

    [Before(Test)]
    public void CreateInBeforeTest(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _beforeTest = MeterCapture.For(MeterName).InCurrentFlow().Add<long>("flow.hits");
    }

    [After(Class)]
    public static void DisposeBeforeClass(ClassHookContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        s_beforeClass?.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _constructed.Dispose();
        _beforeTest?.Dispose();
    }

    [Test]
    public async Task CapturesCreatedInAConstructorOrHook_AreNotScopedToTheTestBody(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Hits.Add(1);

        // Measured on TUnit: the marker set in the constructor, in [Before(Test)] and in [Before(Class)] does not reach the
        // test body, so those captures record nothing of it. Create the capture in the test method or in a synchronous
        // helper it calls. A change here after a TUnit update means the documented limit changed.
        await Assert.That(_constructed.MeasurementCount("flow.hits")).IsEqualTo(0);
        await Assert.That(_beforeTest!.MeasurementCount("flow.hits")).IsEqualTo(0);
        await Assert.That(s_beforeClass!.MeasurementCount("flow.hits")).IsEqualTo(0);
    }
}
