using System;
using System.Globalization;
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

    /// <summary>Writes a carriage return and a line feed as the two characters <c>\r</c> and <c>\n</c>, so a caller-supplied
    /// instrument name cannot split a one-line clause of a failure message.</summary>
    /// <param name="text">The caller-supplied text.</param>
    /// <returns>The text on one line.</returns>
    public static string EscapeLineBreaks(string text)
        => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Returns whether <paramref name="actual"/> equals <paramref name="expected"/> within
    /// <paramref name="tolerance"/>. A value that is not a number never matches, and equal infinities match.</summary>
    /// <param name="actual">The measured value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="tolerance">The allowed absolute difference, already validated as finite and non-negative.</param>
    /// <returns><see langword="true"/> when the values are within the tolerance.</returns>
    public static bool WithinTolerance(double actual, double expected, double tolerance)
    {
        if (double.IsFinite(actual))
            return Math.Abs(actual - expected) <= tolerance;
        if (double.IsPositiveInfinity(actual))
            return double.IsPositiveInfinity(expected);
        return double.IsNegativeInfinity(actual) && double.IsNegativeInfinity(expected);
    }

    /// <summary>
    /// Returns a failing result when the values of <paramref name="captured"/> sum to something no <see cref="long"/> can hold
    /// (not a number, infinite, or beyond the range of a long), because <see cref="MeasurementSet.Total"/> throws
    /// <see cref="OverflowException"/> for such a sum and a failed assertion should read as a failed assertion.
    /// </summary>
    /// <param name="captured">The measurements whose total an assertion is about to read.</param>
    /// <returns>A failing <see cref="AssertionResult"/>, or <see langword="null"/> when the total can be read.</returns>
    public static AssertionResult? TotalOutOfRange(MeasurementSet captured)
    {
        var sum = captured.Sum;
        return double.IsNaN(sum) || sum >= LongRangeLimit || sum < -LongRangeLimit
            ? Failed(captured, string.Concat("the values sum to ", sum.ToString(CultureInfo.InvariantCulture), ", which no long total can hold"))
            : null;
    }

    /// <summary>The magnitude 2^63: a sum at or beyond it (below its negative) does not fit a <see cref="long"/>.</summary>
    private const double LongRangeLimit = 9.2233720368547758E+18d;
}
