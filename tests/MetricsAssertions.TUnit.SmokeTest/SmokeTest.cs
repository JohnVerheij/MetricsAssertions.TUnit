using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using MetricsAssertions;
using TUnit.Core;

namespace Smoke.Consumer;

/// <summary>
/// External-consumer smoke test that verifies the just-packed MetricsAssertions.TUnit NuGet
/// package can be consumed from a deliberately-different namespace (<c>Smoke.Consumer</c>)
/// without leaking into MetricsAssertions.TUnit's internals. Compiles and runs against the
/// local-feed version pinned in <c>NuGet.config</c>, never the in-repo ProjectReference. This is
/// the last CI step before release and the canary that proves the packed nupkg is a usable
/// consumer artifact.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SmokeTest
{
    [Test]
    public async Task ConsumesInstrumentCaptureFromCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var meter = new Meter("Smoke.Consumer.Core");
        var counter = meter.CreateCounter<long>("smoke.count");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(1);

        await Assert.That(capture.Measurements).IsNotNull();
    }

    [Test]
    public async Task ConsumesHasMeasurementCountFromAdapter(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var meter = new Meter("Smoke.Consumer.Adapter");
        var counter = meter.CreateCounter<long>("smoke.count");
        using var capture = InstrumentCapture.Of(counter);

        counter.Add(1);

        await Assert.That(capture).HasMeasurementCount(1);
    }
}
