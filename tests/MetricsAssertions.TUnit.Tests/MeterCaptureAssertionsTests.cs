using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>
/// Tests for the <see cref="MeterCapture"/> adapter assertions, which address one bundled instrument by
/// name (`HasCounterTotal`, `HasUpDownCounterValue`, `HasMeasurementCount`, `HasTaggedMeasurement`): each
/// passes on a match and raises an <see cref="AssertionException"/> on a mismatch.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class MeterCaptureAssertionsTests
{
    [Test]
    public async Task BundledInstrumentAssertions_PassAndFail(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.MC.Meter");
        var placed = meter.CreateCounter<long>("orders.placed");
        using var capture = MeterCapture.For("MetricsAssertions.TUnit.Tests.MC.Meter").Add<long>("orders.placed");

        placed.Add(2, new KeyValuePair<string, object?>("region", "eu"));
        placed.Add(3);

        await Assert.That(capture).HasCounterTotal("orders.placed", 5);
        await Assert.That(capture).HasUpDownCounterValue("orders.placed", 5);
        await Assert.That(capture).HasMeasurementCount("orders.placed", 2);
        await Assert.That(capture).HasTaggedMeasurement("orders.placed", "region", "eu");

        await Assert.That(async () => await Assert.That(capture).HasCounterTotal("orders.placed", 99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasUpDownCounterValue("orders.placed", 99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasMeasurementCount("orders.placed", 99)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasTaggedMeasurement("orders.placed", "region", "us")).Throws<AssertionException>();
    }

    [Test]
    public async Task UpDownCounterAndUnknownInstrument(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.MC.UpDown");
        var udc = meter.CreateUpDownCounter<long>("connections");
        using var capture = MeterCapture.For("MetricsAssertions.TUnit.Tests.MC.UpDown").Add<long>("connections");
        udc.Add(5);
        udc.Add(-2);

        await Assert.That(capture).HasUpDownCounterValue("connections", 3);

        await Assert.That(async () => await Assert.That(capture).HasCounterTotal("nope", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasUpDownCounterValue("nope", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasMeasurementCount("nope", 1)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(capture).HasTaggedMeasurement("nope", "k", "v")).Throws<AssertionException>();
    }
}
