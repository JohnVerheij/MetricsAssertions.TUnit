using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>
/// Real-use pass for the adapter: the assertions where a test suite really calls them (captures built in a constructor or a
/// hook, a retried test, values at the edge of what a total can hold, an instrument that never existed). Each test pins what
/// was measured.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class RealUseAssertionTests : IDisposable
{
    private const string MeterName = "MetricsAssertions.TUnit.Tests.RealUse";

    private static readonly Meter SharedMeter = new(MeterName);
    private static readonly Counter<long> Hits = SharedMeter.CreateCounter<long>("real.hits");
    private static readonly Counter<long> RetryHits = SharedMeter.CreateCounter<long>("real.retry");
    private static MeterCapture? s_beforeClass;

    private readonly MeterCapture _constructed = MeterCapture.For(SharedMeter).Add<long>("real.hits");
    private MeterCapture? _beforeTest;

    [Before(Class)]
    public static void CreateInBeforeClass(ClassHookContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        s_beforeClass = MeterCapture.For(SharedMeter).Add<long>("real.hits");
    }

    [Before(Test)]
    public void CreateInBeforeTest(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _beforeTest = MeterCapture.For(SharedMeter).Add<long>("real.hits");
    }

    [After(Class)]
    public static void DisposeInAfterClass(ClassHookContext context, CancellationToken ct)
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
    public async Task CapturesBuiltInAConstructorAndHooks_SeeTheTestBodysMeasurements(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var before = _constructed.CounterTotal("real.hits");

        Hits.Add(2);

        // A capture by instance does not depend on the flow it was built in: constructor, hooks and test body all see it.
        await Assert.That(_constructed.CounterTotal("real.hits")).IsEqualTo(before + 2);
        await Assert.That(_beforeTest!).HasMeasurementCount("real.hits", 1);
        await Assert.That(s_beforeClass!.CounterTotal("real.hits")).IsGreaterThanOrEqualTo(2L);
    }

    [Test]
    [Retry(2)]
    public async Task Retry_ASecondAttemptWithAFreshCapture_DoesNotSeeTheFirstAttempt(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var attempt = TestContext.Current?.Execution.CurrentRetryAttempt ?? 0;
        using var capture = InstrumentCapture.Of(RetryHits);

        RetryHits.Add(5);
        if (attempt == 0)
            throw new InvalidOperationException("The first attempt fails on purpose, so the test is retried.");

        await Assert.That(capture).HasCounterTotal(5);
        await Assert.That(capture).HasMeasurementCount(1);
    }

    [Test]
    public async Task CounterTotalOfZero_PassesOnACaptureThatNeverAttached(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var capture = MeterCapture.For("MetricsAssertions.TUnit.Tests.RealUse.NoSuchMeter").Add<long>("typo.name");

        // A name typo reads as "nothing was emitted": zero passes. MeterInspector tells the two apart.
        await Assert.That(capture).HasCounterTotal("typo.name", 0);
        await Assert.That(capture).HasMeasurementCount("typo.name", 0);
        await Assert.That(MeterInspector.IsPublished("MetricsAssertions.TUnit.Tests.RealUse.NoSuchMeter", "typo.name")).IsFalse();
        await Assert.That(async () => await Assert.That(capture).HasCounterTotal("typo.name", 1)).Throws<AssertionException>();
    }

    [Test]
    public async Task TotalsThatNoLongCanHold_FailTheAssertion_InsteadOfThrowingOverflow(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".Overflow");
        var big = meter.CreateCounter<long>("big");
        var gauge = meter.CreateHistogram<double>("odd");
        using var capture = MeterCapture.For(meter).Add<long>("big").Add<double>("odd");
        big.Add(long.MaxValue);
        gauge.Record(double.NaN);
        gauge.Record(double.PositiveInfinity);
        var bigSet = capture["big"].Measurements;
        var oddSet = capture["odd"].Measurements;

        await ExpectOutOfRangeAsync(async () => await Assert.That(capture["big"]).HasCounterTotal(long.MaxValue));
        await ExpectOutOfRangeAsync(async () => await Assert.That(capture["big"]).HasCounterTotalAtLeast(1));
        await ExpectOutOfRangeAsync(async () => await Assert.That(capture["big"]).HasUpDownCounterValue(1));
        await ExpectOutOfRangeAsync(async () => await Assert.That(bigSet).HasCounterTotal(long.MaxValue));
        await ExpectOutOfRangeAsync(async () => await Assert.That(bigSet).HasCounterTotalAtLeast(1));
        await ExpectOutOfRangeAsync(async () => await Assert.That(capture).HasCounterTotal("big", 1));
        await ExpectOutOfRangeAsync(async () => await Assert.That(capture).HasUpDownCounterValue("big", 1));
        await ExpectOutOfRangeAsync(async () => await Assert.That(oddSet).HasCounterTotal(0));
        await ExpectOutOfRangeAsync(async () => await Assert.That(capture["odd"]).HasCounterTotalAtLeast(0));
    }

    private static async Task ExpectOutOfRangeAsync(Func<Task> assertion)
    {
        AssertionException? failure = null;
        try
        {
            await assertion();
        }
        catch (AssertionException exception)
        {
            failure = exception;
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Message).Contains("which no long total can hold");
    }

    [Test]
    public async Task HasLastValue_WithNaN_MatchesNaN_ByTheDocumentedExactEquality(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter(MeterName + ".NaN");
        var gauge = meter.CreateHistogram<double>("odd");
        using var capture = InstrumentCapture.Of(gauge);
        gauge.Record(double.NaN);

        await Assert.That(capture).HasLastValue(double.NaN);
        await Assert.That(async () => await Assert.That(capture).HasLastValue(1d)).Throws<AssertionException>();
    }
}
