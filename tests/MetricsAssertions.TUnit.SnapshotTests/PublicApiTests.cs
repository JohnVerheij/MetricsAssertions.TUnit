using System.Threading;
using System.Threading.Tasks;
using PublicApiGenerator;
using SnapshotAssertions.TUnit;

namespace MetricsAssertions.TUnit.SnapshotTests;

/// <summary>
/// Pins the public API surface of both shipped assemblies (<c>MetricsAssertions</c> and
/// <c>MetricsAssertions.TUnit</c>) using <c>SnapshotAssertions.TUnit</c>'s <c>MatchesSnapshot()</c>
/// chain. Any change to a public type, member, signature, attribute, or visibility produces a
/// diff against the <c>.expected.txt</c> baseline under <c>Snapshots/</c> and fails the test
/// until the snapshot is explicitly re-accepted (write the new content to the expected path,
/// or run with <c>SNAPSHOT_ACCEPT=1</c> to auto-write).
/// </summary>
/// <remarks>
/// Stronger than ApiCompat's per-version baseline check because this snapshot fires on every
/// PR, not just at pack time.
/// </remarks>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class PublicApiTests
{
    [Test]
    public async Task MetricsAssertionsPublicApiHasNotChangedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var assembly = typeof(global::MetricsAssertions.InstrumentCapture).Assembly;
        // Normalize line endings so the snapshot baseline survives both Linux CI (LF native)
        // and Windows local dev (CRLF native).
        var publicApi = assembly.GeneratePublicApi().ReplaceLineEndings("\n");

        await Assert.That(publicApi).MatchesSnapshot();
    }

    [Test]
    public async Task MetricsAssertionsTUnitPublicApiHasNotChangedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var assembly = typeof(global::MetricsAssertions.TUnit.InstrumentCaptureAssertions).Assembly;
        var publicApi = assembly.GeneratePublicApi().ReplaceLineEndings("\n");

        await Assert.That(publicApi).MatchesSnapshot();
    }
}
