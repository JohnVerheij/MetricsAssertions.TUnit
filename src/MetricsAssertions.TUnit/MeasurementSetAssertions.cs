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
        => string.Join(", ", values.Select(static v => v.ToString(CultureInfo.InvariantCulture)));

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static void ValidateTolerance(double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be a finite, non-negative number.");
    }

    /// <summary>Describes the samples of a set and the expected ones for a failure message; the expected samples are part of
    /// the actual text because a list cannot be a placeholder of the expectation message.</summary>
    /// <param name="set">The measurement set the assertion ran on.</param>
    /// <param name="expected">The expected samples.</param>
    /// <returns>The actual samples next to the expected ones.</returns>
    private static string SamplesActual(MeasurementSet set, IEnumerable<double> expected)
        => string.Concat("the samples were [", Fmt(set.Values), "], expected [", Fmt(expected), "]");

    /// <summary>Asserts the net total of the values equals <paramref name="expected"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected net total.</param>
    /// <remarks>A counter never decrements, so the assertion fails when a negative delta was recorded; use <c>HasUpDownCounterValue</c> for an up-down counter. It also fails, with a message, when the values sum to something no <see cref="long"/> can hold (not a number, infinite, or beyond the range of a long). A capture that recorded nothing has a total of 0, so a total of 0 passes on an empty capture; <see cref="MeterInspector"/> tells "nothing was emitted" from "the instrument does not exist".</remarks>
    /// <returns>A passing assertion when the total matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have counter total {expected}", InlineMethodBody = false)]
    public static AssertionResult HasCounterTotal(this MeasurementSet value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Min < 0)
            return MeasurementDiagnostics.Failed(value,
                "the set contains a negative value, which a counter never has");
        if (MeasurementDiagnostics.TotalOutOfRange(value) is { } outOfRange)
            return outOfRange;
        return value.Total == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat("it was ", value.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the net total of the values is at least <paramref name="expected"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The minimum expected net total.</param>
    /// <remarks>A counter never decrements, so the assertion fails when a negative delta was recorded; use <c>HasUpDownCounterValue</c> for an up-down counter. It also fails, with a message, when the values sum to something no <see cref="long"/> can hold (not a number, infinite, or beyond the range of a long). A capture that recorded nothing has a total of 0, so a total of 0 passes on an empty capture; <see cref="MeterInspector"/> tells "nothing was emitted" from "the instrument does not exist".</remarks>
    /// <returns>A passing assertion when the total is at least the expected; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have counter total of at least {expected}", InlineMethodBody = false)]
    public static AssertionResult HasCounterTotalAtLeast(this MeasurementSet value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Min < 0)
            return MeasurementDiagnostics.Failed(value,
                "the set contains a negative value, which a counter never has");
        if (MeasurementDiagnostics.TotalOutOfRange(value) is { } outOfRange)
            return outOfRange;
        return value.Total >= expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat("it was ", value.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the number of measurements equals <paramref name="expected"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected measurement count.</param>
    /// <returns>A passing assertion when the count matches; otherwise a failing one naming both counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have {expected} measurement(s)", InlineMethodBody = false)]
    public static AssertionResult HasMeasurementCount(this MeasurementSet value, int expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Count == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat("it had ", value.Count.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the set is empty.</summary>
    /// <param name="value">The measurement set.</param>
    /// <returns>A passing assertion when the set is empty; otherwise a failing one naming the count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have no measurements", InlineMethodBody = false)]
    public static AssertionResult HasNoMeasurements(this MeasurementSet value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.IsEmpty
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat("it had ", value.Count.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the sum of the histogram samples equals <paramref name="expected"/> (within
    /// <paramref name="tolerance"/>).</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected sum, in the unit of the captured instrument.</param>
    /// <param name="tolerance">The allowed absolute difference, in the unit of the captured instrument (exact by default).</param>
    /// <remarks>A sum that is not a number never matches; an infinite sum matches an equal infinity.</remarks>
    /// <returns>A passing assertion when the sum is within tolerance; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> is negative or non-finite.</exception>
    [GenerateAssertion(ExpectationMessage = "to have a sample sum of {expected} (within {tolerance})", InlineMethodBody = false)]
    public static AssertionResult HasSampleSum(this MeasurementSet value, double expected, double tolerance = 0d)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateTolerance(tolerance);
        var sum = value.Sum;
        return MeasurementDiagnostics.WithinTolerance(sum, expected, tolerance)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat("it was ", Num(sum)));
    }

    /// <summary>Asserts the mean of the histogram samples equals <paramref name="expected"/> (within
    /// <paramref name="tolerance"/>).</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected average.</param>
    /// <param name="tolerance">The allowed absolute difference, in the unit of the captured instrument (exact by default).</param>
    /// <remarks>A set with no samples fails: the average of nothing is undefined, and <see cref="MeasurementSet.Average"/> reports 0 for it, which would match an expected 0. An average that is not a number never matches.</remarks>
    /// <returns>A passing assertion when the average is within tolerance; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> is negative or non-finite.</exception>
    [GenerateAssertion(ExpectationMessage = "to have a sample average of {expected} (within {tolerance})", InlineMethodBody = false)]
    public static AssertionResult HasSampleAverage(this MeasurementSet value, double expected, double tolerance = 0d)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateTolerance(tolerance);
        if (value.IsEmpty)
            return MeasurementDiagnostics.Failed(value, "it had no samples, so there is no average");
        var average = value.Average;
        return MeasurementDiagnostics.WithinTolerance(average, expected, tolerance)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat("it was ", Num(average)));
    }

    /// <summary>Asserts every sample lies within the inclusive range [<paramref name="min"/>,
    /// <paramref name="max"/>].</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <remarks>A set with no samples fails: nothing was measured, so nothing can be said to be in range. A sample that is not a number is never in range.</remarks>
    /// <returns>A passing assertion when every sample is in range; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have all samples within [{min}, {max}]", InlineMethodBody = false)]
    public static AssertionResult HasAllSamplesInRange(this MeasurementSet value, double min, double max)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);
        if (value.IsEmpty)
            return MeasurementDiagnostics.Failed(value, "it had no samples, so none can be within the range");
        return value.AllValuesInRange(min, max)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, string.Concat("the samples were [", Fmt(value.Values), "]"));
    }

    /// <summary>Asserts the samples equal <paramref name="expected"/> in order.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected samples, in order.</param>
    /// <returns>A passing assertion when the samples match in order; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="expected"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have the expected samples in order", InlineMethodBody = false)]
    public static AssertionResult HasSamples(this MeasurementSet value, params double[] expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(expected);
        return value.SamplesEqual(expected)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, SamplesActual(value, expected));
    }

    /// <summary>Asserts the samples equal <paramref name="expected"/> in order, each within
    /// <paramref name="tolerance"/> (absolute), the tolerant counterpart of
    /// <see cref="HasSamples(MeasurementSet, double[])"/> for samples fed computed doubles.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected samples, in order.</param>
    /// <param name="tolerance">The allowed absolute difference per sample, in the unit of the captured instrument.</param>
    /// <returns>A passing assertion when the samples match in order within tolerance; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="expected"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> is negative or non-finite.</exception>
    [GenerateAssertion(ExpectationMessage = "to have the expected samples in order, each within {tolerance}", InlineMethodBody = false)]
    public static AssertionResult HasSamples(this MeasurementSet value, IReadOnlyList<double> expected, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(expected);
        ValidateTolerance(tolerance);
        return value.SamplesEqual(expected, tolerance)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, SamplesActual(value, expected));
    }

    /// <summary>Asserts the samples equal <paramref name="expected"/> regardless of order.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected samples, in any order.</param>
    /// <returns>A passing assertion when the samples match in any order; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="expected"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have the expected samples in any order", InlineMethodBody = false)]
    public static AssertionResult HasSamplesInAnyOrder(this MeasurementSet value, params double[] expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(expected);
        return value.SamplesEquivalentTo(expected)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, SamplesActual(value, expected));
    }

    /// <summary>Asserts the samples equal <paramref name="expected"/> regardless of order, each within
    /// <paramref name="tolerance"/> (absolute), the tolerant counterpart of
    /// <see cref="HasSamplesInAnyOrder(MeasurementSet, double[])"/> for samples fed computed doubles.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="expected">The expected samples, in any order.</param>
    /// <param name="tolerance">The allowed absolute difference per sample, in the unit of the captured instrument.</param>
    /// <returns>A passing assertion when the samples match in any order within tolerance; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="expected"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> is negative or non-finite.</exception>
    [GenerateAssertion(ExpectationMessage = "to have the expected samples in any order, each within {tolerance}", InlineMethodBody = false)]
    public static AssertionResult HasSamplesInAnyOrder(this MeasurementSet value, IReadOnlyList<double> expected, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(expected);
        ValidateTolerance(tolerance);
        return value.SamplesEquivalentTo(expected, tolerance)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, SamplesActual(value, expected));
    }

    /// <summary>Asserts every measurement carries a tag with key <paramref name="tagKey"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="tagKey">The tag key that must be present on every measurement.</param>
    /// <returns>A passing assertion when every measurement is tagged; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="tagKey"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have every measurement tagged with {tagKey}", InlineMethodBody = false)]
    public static AssertionResult HasEveryMeasurementTagged(this MeasurementSet value, string tagKey)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(tagKey);
        return value.EveryMeasurementCarriesTag(tagKey)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, "not all were");
    }

    /// <summary>Asserts at least one measurement carries a tag <paramref name="tagKey"/> equal to
    /// <paramref name="tagValue"/>.</summary>
    /// <param name="value">The measurement set.</param>
    /// <param name="tagKey">The tag key to match.</param>
    /// <param name="tagValue">The tag value to match.</param>
    /// <returns>A passing assertion when a matching measurement exists; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="tagKey"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have a measurement tagged {tagKey}={tagValue}", InlineMethodBody = false)]
    public static AssertionResult HasTaggedMeasurement(this MeasurementSet value, string tagKey, object? tagValue)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(tagKey);
        return value.Tagged(tagKey, tagValue).Count > 0
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value, "none matched");
    }
}
