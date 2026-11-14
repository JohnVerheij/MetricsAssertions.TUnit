using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MetricsAssertions;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace MetricsAssertions.TUnit;

/// <summary>
/// TUnit-native fluent <c>Assert.That(capture).Has*</c> assertions over a single-instrument
/// <see cref="InstrumentCapture"/>: counter and up-down-counter totals, measurement counts, last value,
/// and tag presence. Generated via the TUnit <c>[GenerateAssertion]</c> source generator (AOT-clean).
/// </summary>
public static class InstrumentCaptureAssertions
{
    /// <summary>Asserts the net total of the captured counter equals <paramref name="expected"/>.</summary>
    /// <param name="value">The instrument capture, as the receiver of the fluent assertion.</param>
    /// <param name="expected">The expected net total.</param>
    /// <remarks>A counter never decrements, so the assertion fails when a negative delta was recorded; use <c>HasUpDownCounterValue</c> for an up-down counter. It also fails, with a message, when the values sum to something no <see cref="long"/> can hold (not a number, infinite, or beyond the range of a long). A capture that recorded nothing has a total of 0, so a total of 0 passes on an empty capture; <see cref="MeterInspector"/> tells "nothing was emitted" from "the instrument does not exist".</remarks>
    /// <returns>A passing assertion when the total matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have counter total {expected}", InlineMethodBody = false)]
    public static AssertionResult HasCounterTotal(this InstrumentCapture value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        MeasurementSet set = value.Measurements;
        if (set.Min < 0)
            return MeasurementDiagnostics.Failed(set,
                "the capture recorded a negative delta, which a counter never does");
        if (MeasurementDiagnostics.TotalOutOfRange(set) is { } outOfRange)
            return outOfRange;
        return set.Total == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(set, string.Concat("it was ", set.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the net total of the captured counter is at least <paramref name="expected"/>.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The minimum expected net total.</param>
    /// <remarks>A counter never decrements, so the assertion fails when a negative delta was recorded; use <c>HasUpDownCounterValue</c> for an up-down counter. It also fails, with a message, when the values sum to something no <see cref="long"/> can hold (not a number, infinite, or beyond the range of a long). A capture that recorded nothing has a total of 0, so a total of 0 passes on an empty capture; <see cref="MeterInspector"/> tells "nothing was emitted" from "the instrument does not exist".</remarks>
    /// <returns>A passing assertion when the total is at least the expected; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have counter total of at least {expected}", InlineMethodBody = false)]
    public static AssertionResult HasCounterTotalAtLeast(this InstrumentCapture value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        MeasurementSet set = value.Measurements;
        if (set.Min < 0)
            return MeasurementDiagnostics.Failed(set,
                "the capture recorded a negative delta, which a counter never does");
        if (MeasurementDiagnostics.TotalOutOfRange(set) is { } outOfRange)
            return outOfRange;
        return set.Total >= expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(set, string.Concat("it was ", set.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the net value of the captured up-down counter equals <paramref name="expected"/>.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The expected net value.</param>
    /// <remarks>Unlike the counter assertions, negative deltas are part of the value. The assertion fails, with a message, when the values sum to something no <see cref="long"/> can hold.</remarks>
    /// <returns>A passing assertion when the value matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have up-down counter value {expected}", InlineMethodBody = false)]
    public static AssertionResult HasUpDownCounterValue(this InstrumentCapture value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        MeasurementSet set = value.Measurements;
        if (MeasurementDiagnostics.TotalOutOfRange(set) is { } outOfRange)
            return outOfRange;
        return set.Total == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(set, string.Concat("it was ", set.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts that exactly <paramref name="expected"/> measurements were captured.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The expected number of captured measurements.</param>
    /// <returns>A passing assertion when the count matches; otherwise a failing one naming both counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have {expected} measurement(s)", InlineMethodBody = false)]
    public static AssertionResult HasMeasurementCount(this InstrumentCapture value, int expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Count == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value.Measurements, string.Concat("it had ", value.Count.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts that no measurements were captured.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <returns>A passing assertion when the capture is empty; otherwise a failing one naming the count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have no measurements", InlineMethodBody = false)]
    public static AssertionResult HasNoMeasurements(this InstrumentCapture value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Count is 0
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value.Measurements, string.Concat("it had ", value.Count.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the most recently captured value equals <paramref name="expected"/>.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The expected last value.</param>
    /// <returns>A passing assertion when the last value matches; otherwise a failing one (also failing when
    /// no measurements were captured).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [SuppressMessage(
        "Major Bug",
        "S1244:Do not check floating point equality with exact values",
        Justification = "Exact equality is the documented contract of this assertion: the caller supplies the value to match. A tolerance would pass measurements the caller did not ask for, with no way to opt out.")]
    [GenerateAssertion(ExpectationMessage = "to have last value {expected}", InlineMethodBody = false)]
    public static AssertionResult HasLastValue(this InstrumentCapture value, double expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.LastValue is { } last && last.Equals(expected)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value.Measurements, LastValueActual(value));
    }

    /// <summary>Asserts the most recently captured value equals <paramref name="expected"/> within
    /// <paramref name="tolerance"/> (absolute), the tolerant counterpart of
    /// <see cref="HasLastValue(InstrumentCapture, double)"/> for gauges fed computed doubles.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The expected last value.</param>
    /// <param name="tolerance">The allowed absolute difference, in the unit of the captured instrument.</param>
    /// <returns>A passing assertion when the last value is within tolerance; otherwise a failing one
    /// (also failing when no measurements were captured).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> is negative or
    /// non-finite.</exception>
    [GenerateAssertion(ExpectationMessage = "to have last value {expected} (within {tolerance})", InlineMethodBody = false)]
    public static AssertionResult HasLastValue(this InstrumentCapture value, double expected, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!double.IsFinite(tolerance) || tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be a finite, non-negative number.");
        return value.LastValue is { } last && Math.Abs(last - expected) <= tolerance
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value.Measurements, LastValueActual(value));
    }

    /// <summary>Describes the last captured value for a failure message.</summary>
    /// <param name="capture">The capture the assertion ran on.</param>
    /// <returns>The last value, or a statement that nothing was captured.</returns>
    private static string LastValueActual(InstrumentCapture capture)
        => capture.LastValue is { } last
            ? string.Concat("it was ", last.ToString(CultureInfo.InvariantCulture))
            : "it had no measurements";

    /// <summary>Asserts at least one captured measurement carries a tag <paramref name="tagKey"/> equal to
    /// <paramref name="tagValue"/>.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="tagKey">The tag key to match.</param>
    /// <param name="tagValue">The tag value to match.</param>
    /// <returns>A passing assertion when a matching measurement exists; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="tagKey"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have a measurement tagged {tagKey}={tagValue}", InlineMethodBody = false)]
    public static AssertionResult HasTaggedMeasurement(this InstrumentCapture value, string tagKey, object? tagValue)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(tagKey);
        return value.HasMeasurementTagged(tagKey, tagValue)
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(value.Measurements, "none matched");
    }
}
