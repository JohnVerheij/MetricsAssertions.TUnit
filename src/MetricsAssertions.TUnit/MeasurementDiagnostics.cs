using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

    /// <summary>Writes a backslash as <c>\\</c>, then a carriage return and a line feed as the two characters <c>\r</c> and
    /// <c>\n</c>, so a caller-supplied instrument name cannot split a one-line clause of a failure message, a real line
    /// break never reads like a backslash followed by <c>n</c>, and the name reads as it does in the captured listing
    /// under the failure (<see cref="MeasurementSet.Describe"/> escapes the same way).</summary>
    /// <param name="text">The caller-supplied text.</param>
    /// <returns>The text on one line.</returns>
    public static string EscapeLineBreaks(string text)
        => text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

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

    /// <summary>The most entries a listing under a failure shows; it says how many more there are.</summary>
    internal const int MaxListed = 20;

    /// <summary>
    /// Checks that the tag <paramref name="tagKey"/> has at most <paramref name="max"/> distinct values in
    /// <paramref name="set"/>, failing when no measurement carries the tag, so a misspelled key or an empty capture does
    /// not pass.
    /// </summary>
    /// <param name="set">The measurements whose tag values are counted.</param>
    /// <param name="dump">The measurements to render under a failure, read in the same snapshot as <paramref name="set"/>.</param>
    /// <param name="tagKey">The tag key.</param>
    /// <param name="max">The most distinct values allowed.</param>
    /// <returns>A passing result, or a failing one that lists the values.</returns>
    public static AssertionResult TagCardinalityAtMost(MeasurementSet set, MeasurementSet dump, string tagKey, int max)
    {
        IReadOnlyList<string> values = set.DistinctTagValues(tagKey);
        if (values.Count is 0)
            return Failed(dump, "no measurement carried the tag");
        return values.Count <= max
            ? AssertionResult.Passed
            : Failed(dump, string.Concat(
                "it had ", values.Count.ToString(CultureInfo.InvariantCulture), Listing("values", [.. values.Select(Quote)])));
    }

    /// <summary>
    /// Checks that <paramref name="set"/> holds at most <paramref name="max"/> series, failing when it holds no
    /// measurements, so a capture that recorded nothing does not pass.
    /// </summary>
    /// <param name="set">The measurements whose series are counted.</param>
    /// <param name="dump">The measurements to render under a failure, read in the same snapshot as <paramref name="set"/>.</param>
    /// <param name="max">The most series allowed.</param>
    /// <returns>A passing result, or a failing one that lists the series.</returns>
    public static AssertionResult SeriesCountAtMost(MeasurementSet set, MeasurementSet dump, int max)
    {
        if (set.IsEmpty)
            return Failed(dump, "it had no measurements");
        IReadOnlyList<string> series = set.DistinctSeries();
        return series.Count <= max
            ? AssertionResult.Passed
            : Failed(dump, string.Concat("it had ", series.Count.ToString(CultureInfo.InvariantCulture), Listing("series", series)));
    }

    /// <summary>Renders a titled listing for a failure message, one entry per line, at most <see cref="MaxListed"/>
    /// entries followed by how many more there are.</summary>
    /// <param name="heading">The title of the listing.</param>
    /// <param name="entries">The entries, already on one line each.</param>
    /// <returns>The listing, starting with a line break.</returns>
    private static string Listing(string heading, IReadOnlyList<string> entries)
    {
        var sb = new StringBuilder("\n  ").Append(heading).Append(':');
        var shown = Math.Min(entries.Count, MaxListed);
        for (int i = 0; i < shown; i++)
            sb.Append("\n    ").Append(entries[i]);
        if (entries.Count > shown)
            sb.Append("\n    ... and ").Append((entries.Count - shown).ToString(CultureInfo.InvariantCulture)).Append(" more");
        return sb.ToString();
    }

    /// <summary>Quotes a tag value for a listing, so an empty value and leading or trailing spaces show: a backslash, a
    /// line break and a double quote inside it are escaped.</summary>
    /// <param name="text">The tag value text.</param>
    /// <returns>The value in double quotes, on one line.</returns>
    private static string Quote(string text)
        => string.Concat("\"", EscapeLineBreaks(text).Replace("\"", "\\\"", StringComparison.Ordinal), "\"");
}
