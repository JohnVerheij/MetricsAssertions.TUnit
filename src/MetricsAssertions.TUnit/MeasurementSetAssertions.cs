using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MetricsAssertions;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace MetricsAssertions.TUnit;

/// <summary>
/// TUnit-native fluent <c>Assert.That(set).Has*</c> assertions over a <see cref="MeasurementSet"/>:
/// counter totals, measurement counts, emptiness, histogram sample sum / average / range, exact and
/// order-insensitive sample sets, and tag-consistency. Narrow first with
/// <see cref="MeasurementSet.Tagged(string, object?)"/> / <see cref="MeasurementSet.ForInstrument"/> to
/// assert a dimension. Generated via the TUnit <c>[GenerateAssertion]</c> source generator (AOT-clean).
/// </summary>
public static class MeasurementSetAssertions
{
    private static string Fmt(IEnumerable<double> values)
        => string.Join(", ", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static void ValidateTolerance(double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be a finite, non-negative number.");
    }

    /// <summary>Asserts the net total of the values equals <paramref name="expected"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected net total.</param>
    /// <returns>A passing assertion when the total matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasCounterTotal(this MeasurementSet value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Min < 0)
            return MeasurementDiagnostics.Failed(value,
                "a counter never decrements, but the set contains a negative value");
        return value.Total == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have a total of ", expected.ToString(CultureInfo.InvariantCulture),
                "\n  but it was ", value.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the net total of the values is at least <paramref name="expected"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The minimum expected net total.</param>
    /// <returns>A passing assertion when the total is at least the expected; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasCounterTotalAtLeast(this MeasurementSet value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Min < 0)
            return MeasurementDiagnostics.Failed(value,
                "a counter never decrements, but the set contains a negative value");
        return value.Total >= expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have a total of at least ", expected.ToString(CultureInfo.InvariantCulture),
                "\n  but it was ", value.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the number of measurements equals <paramref name="expected"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected measurement count.</param>
    /// <returns>A passing assertion when the count matches; otherwise a failing one naming both counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasMeasurementCount(this MeasurementSet value, int expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Count == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have ", expected.ToString(CultureInfo.InvariantCulture),
                " measurement(s)\n  but it had ", value.Count.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the set is empty.</summary>
    /// <param name="value">The measurement set.</param>
    /// <returns>A passing assertion when the set is empty; otherwise a failing one naming the count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasNoMeasurements(this MeasurementSet value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.IsEmpty
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to be empty\n  but it had ",
                value.Count.ToString(CultureInfo.InvariantCulture), " measurement(s)"));
    }

    /// <summary>Asserts the sum of the histogram samples equals <paramref name="expected"/> (within
    /// <paramref name="tolerance"/>).</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected sum.</param>
    /// <param name="tolerance">The allowed absolute difference (exact by default).</param>
    /// <returns>A passing assertion when the sum is within tolerance; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasSampleSum(this MeasurementSet value, double expected, double tolerance = 0d)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateTolerance(tolerance);
        return Math.Abs(value.Sum - expected) <= tolerance
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have a sample sum of ", Num(expected), "\n  but it was ", Num(value.Sum)));
    }

    /// <summary>Asserts the mean of the histogram samples equals <paramref name="expected"/> (within
    /// <paramref name="tolerance"/>).</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected average.</param>
    /// <param name="tolerance">The allowed absolute difference (exact by default).</param>
    /// <returns>A passing assertion when the average is within tolerance; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasSampleAverage(this MeasurementSet value, double expected, double tolerance = 0d)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateTolerance(tolerance);
        return Math.Abs(value.Average - expected) <= tolerance
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have a sample average of ", Num(expected), "\n  but it was ", Num(value.Average)));
    }

    /// <summary>Asserts every sample lies within the inclusive range [<paramref name="min"/>,
    /// <paramref name="max"/>].</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>A passing assertion when every sample is in range; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasAllSamplesInRange(this MeasurementSet value, double min, double max)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);
        return value.AllValuesInRange(min, max)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have all samples within [", Num(min), ", ", Num(max),
                "]\n  but it was [", Fmt(value.Values), "]"));
    }

    /// <summary>Asserts the samples equal <paramref name="expected"/> in order.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected samples, in order.</param>
    /// <returns>A passing assertion when the samples match in order; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="expected"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasSamples(this MeasurementSet value, params double[] expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(expected);
        return value.SamplesEqual(expected)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have samples [", Fmt(expected), "] in order\n  but it had [", Fmt(value.Values), "]"));
    }

    /// <summary>Asserts the samples equal <paramref name="expected"/> regardless of order.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected samples, in any order.</param>
    /// <returns>A passing assertion when the samples match in any order; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="expected"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasSamplesInAnyOrder(this MeasurementSet value, params double[] expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(expected);
        return value.SamplesEquivalentTo(expected)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have samples [", Fmt(expected), "] in any order\n  but it had [", Fmt(value.Values), "]"));
    }

    /// <summary>Asserts every measurement carries a tag with key <paramref name="tagKey"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="tagKey">The tag key that must be present on every measurement.</param>
    /// <returns>A passing assertion when every measurement is tagged; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="tagKey"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasEveryMeasurementTagged(this MeasurementSet value, string tagKey)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(tagKey);
        return value.EveryMeasurementCarriesTag(tagKey)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have every measurement tagged with ", tagKey, "\n  but not all were"));
    }

    /// <summary>Asserts at least one measurement carries a tag <paramref name="tagKey"/> equal to
    /// <paramref name="tagValue"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="tagKey">The tag key to match.</param>
    /// <param name="tagValue">The tag value to match.</param>
    /// <returns>A passing assertion when a matching measurement exists; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="tagKey"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasTaggedMeasurement(this MeasurementSet value, string tagKey, object? tagValue)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(tagKey);
        return value.Tagged(tagKey, tagValue).Count > 0
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat(
                "the set to have a measurement tagged ", tagKey, "=",
                Convert.ToString(tagValue, CultureInfo.InvariantCulture), "\n  but none matched"));
    }
}
