using System;
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
    /// <returns>A passing assertion when the total matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasCounterTotal(this InstrumentCapture value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Total == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have counter total ", expected.ToString(CultureInfo.InvariantCulture),
                "\n  but it was ", value.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the net total of the captured counter is at least <paramref name="expected"/>.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The minimum expected net total.</param>
    /// <returns>A passing assertion when the total is at least the expected; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasCounterTotalAtLeast(this InstrumentCapture value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Total >= expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have counter total at least ", expected.ToString(CultureInfo.InvariantCulture),
                "\n  but it was ", value.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the net value of the captured up-down counter equals <paramref name="expected"/>.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The expected net value.</param>
    /// <returns>A passing assertion when the value matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasUpDownCounterValue(this InstrumentCapture value, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Total == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have up-down counter value ", expected.ToString(CultureInfo.InvariantCulture),
                "\n  but it was ", value.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts that exactly <paramref name="expected"/> measurements were captured.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The expected number of captured measurements.</param>
    /// <returns>A passing assertion when the count matches; otherwise a failing one naming both counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasMeasurementCount(this InstrumentCapture value, int expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Count == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have captured ", expected.ToString(CultureInfo.InvariantCulture),
                " measurement(s)\n  but it had ", value.Count.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts that no measurements were captured.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <returns>A passing assertion when the capture is empty; otherwise a failing one naming the count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasNoMeasurements(this InstrumentCapture value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Count is 0
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have recorded no measurements\n  but it had ",
                value.Count.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the most recently captured value equals <paramref name="expected"/>.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="expected">The expected last value.</param>
    /// <returns>A passing assertion when the last value matches; otherwise a failing one (also failing when
    /// no measurements were captured).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasLastValue(this InstrumentCapture value, double expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.LastValue is { } last && last.Equals(expected)
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have last value ", expected.ToString(CultureInfo.InvariantCulture),
                "\n  but it was ", value.LastValue?.ToString(CultureInfo.InvariantCulture) ?? "(no measurements)"));
    }

    /// <summary>Asserts at least one captured measurement carries a tag <paramref name="tagKey"/> equal to
    /// <paramref name="tagValue"/>.</summary>
    /// <param name="value">The instrument capture.</param>
    /// <param name="tagKey">The tag key to match.</param>
    /// <param name="tagValue">The tag value to match.</param>
    /// <returns>A passing assertion when a matching measurement exists; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="tagKey"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasTaggedMeasurement(this InstrumentCapture value, string tagKey, object? tagValue)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(tagKey);
        return value.HasMeasurementTagged(tagKey, tagValue)
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have a measurement tagged ", tagKey, "=",
                Convert.ToString(tagValue, CultureInfo.InvariantCulture), "\n  but none matched"));
    }
}
