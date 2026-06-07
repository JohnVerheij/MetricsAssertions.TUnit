using MetricsAssertions;
using TUnit.Assertions.Core;

namespace MetricsAssertions.TUnit;

/// <summary>
/// Builds failing <see cref="AssertionResult"/>s that append a dump of the captured measurements
/// (instrument, value, tags, timestamp) under the failure, so a mismatch shows what was actually
/// recorded instead of only the mismatched scalar. This is the family's "show the captured items"
/// diagnostic, applied to every metric assertion.
/// </summary>
internal static class MeasurementDiagnostics
{
    /// <summary>
    /// Creates a failing result whose message is <paramref name="expectation"/> followed by a rendered
    /// dump of <paramref name="captured"/>.
    /// </summary>
    /// <param name="captured">The measurements to render under the failure.</param>
    /// <param name="expectation">The expectation-and-actual portion of the failure message.</param>
    /// <returns>A failing <see cref="AssertionResult"/> carrying both.</returns>
    public static AssertionResult Failed(MeasurementSet captured, string expectation)
        => AssertionResult.Failed(string.Concat(expectation, "\n  captured:\n", captured.Describe()));
}
