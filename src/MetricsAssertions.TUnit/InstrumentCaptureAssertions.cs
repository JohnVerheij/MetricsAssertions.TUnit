using System;
using System.Globalization;
using MetricsAssertions;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace MetricsAssertions.TUnit;

/// <summary>
/// TUnit-native fluent <c>Assert.That(capture).Has*</c> assertions over a single-instrument
/// <see cref="InstrumentCapture"/>.
/// </summary>
/// <remarks>
/// Source methods carry the <c>[GenerateAssertion]</c> attribute; TUnit's source generator emits the
/// fluent <c>Assert.That(capture).&lt;Method&gt;()</c> entry point at consumer build time. The generated
/// chain is AOT-clean (no runtime reflection in the assertion path). The foundation release (v0.0.1) ships
/// <see cref="HasMeasurementCount"/>; the full surface (counter and up-down-counter totals, tag-value
/// queries, captured-after-baseline deltas, and meter-wide assertions) lands in 0.1.0.
/// </remarks>
public static class InstrumentCaptureAssertions
{
    /// <summary>Asserts that exactly <paramref name="expected"/> measurements were captured.</summary>
    /// <param name="value">The instrument capture, as the receiver of the fluent assertion.</param>
    /// <param name="expected">The expected number of captured measurements.</param>
    /// <returns>A passing assertion when the captured count equals <paramref name="expected"/>; otherwise
    /// a failing assertion naming the expected and observed counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasMeasurementCount(this InstrumentCapture value, int expected)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Count == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have captured ",
                expected.ToString(CultureInfo.InvariantCulture),
                " measurement(s)\n  but it had ",
                value.Count.ToString(CultureInfo.InvariantCulture)));
    }
}
