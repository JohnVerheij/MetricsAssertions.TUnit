using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>
/// Tests for the TUnit adapter assertion <c>HasMeasurementCount</c>, generated via
/// <c>[GenerateAssertion]</c> over <see cref="InstrumentCapture"/>: it passes on an exact captured
/// count and fails (raising an <see cref="AssertionException"/>) on a mismatch.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class InstrumentCaptureAssertionsTests
{
    [Test]
    public async Task HasMeasurementCount_Matches_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.CountPass");
        var counter = meter.CreateCounter<int>("hits");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(1);
        counter.Add(1);

        await Assert.That(capture).HasMeasurementCount(2);
    }

    [Test]
    public async Task HasMeasurementCount_Mismatch_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var meter = new Meter("MetricsAssertions.TUnit.Tests.CountFail");
        var counter = meter.CreateCounter<int>("hits");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(1);

        await Assert.That(async () => await Assert.That(capture).HasMeasurementCount(2))
            .Throws<AssertionException>();
    }
}
