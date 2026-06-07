using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// Tests for <see cref="MeterInspector"/>: enumerating a meter's published instruments
/// (<see cref="MeterInspector.PublishedInstrumentNames"/>, including that instruments on other meters are
/// filtered out), <see cref="MeterInspector.IsPublished"/> for present and absent instruments,
/// <see cref="MeterInspector.PublishesAll"/> for the all-present and some-missing cases, and the
/// null-argument guards.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class MeterInspectorTests
{
    [Test]
    public async Task PublishedInstrumentNames_ReturnsOwnInstrumentsOnly(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var target = new Meter("MetricsAssertions.Tests.Inspect.Target");
        var c1 = target.CreateCounter<long>("alpha");
        var c2 = target.CreateCounter<long>("beta");
        using var other = new Meter("MetricsAssertions.Tests.Inspect.Other");
        var c3 = other.CreateCounter<long>("gamma");
        _ = (c1, c2, c3);

        var names = MeterInspector.PublishedInstrumentNames("MetricsAssertions.Tests.Inspect.Target");

        await Assert.That(names).Contains("alpha");
        await Assert.That(names).Contains("beta");
        await Assert.That(names).DoesNotContain("gamma");
    }

    [Test]
    public async Task IsPublished_TrueForPresent_FalseForAbsent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Inspect.Is");
        var c = meter.CreateCounter<long>("present");
        _ = c;

        await Assert.That(MeterInspector.IsPublished("MetricsAssertions.Tests.Inspect.Is", "present")).IsTrue();
        await Assert.That(MeterInspector.IsPublished("MetricsAssertions.Tests.Inspect.Is", "absent")).IsFalse();
    }

    [Test]
    public async Task PublishesAll_TrueWhenAllPresent_FalseWhenOneMissing(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.Tests.Inspect.All");
        var a = meter.CreateCounter<long>("a");
        var b = meter.CreateCounter<long>("b");
        _ = (a, b);

        await Assert.That(MeterInspector.PublishesAll("MetricsAssertions.Tests.Inspect.All", "a", "b")).IsTrue();
        await Assert.That(MeterInspector.PublishesAll("MetricsAssertions.Tests.Inspect.All", "a", "missing")).IsFalse();
    }

    [Test]
    public async Task NullArguments_Throw(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Assert.That(() => MeterInspector.PublishedInstrumentNames(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => MeterInspector.PublishesAll("m", null!)).Throws<ArgumentNullException>();
    }
}
