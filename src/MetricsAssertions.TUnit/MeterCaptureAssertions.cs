using System;
using System.Globalization;
using MetricsAssertions;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace MetricsAssertions.TUnit;

/// <summary>
/// TUnit-native fluent <c>Assert.That(meter).Has*</c> assertions over a multi-instrument
/// <see cref="MeterCapture"/>, addressing one bundled instrument by name. Generated via the TUnit
/// <c>[GenerateAssertion]</c> source generator (AOT-clean).
/// </summary>
public static class MeterCaptureAssertions
{
    /// <summary>Asserts the net total of a bundled counter equals <paramref name="expected"/>.</summary>
    /// <param name="value">The meter capture.</param>
    /// <param name="instrumentName">The counter instrument name.</param>
    /// <param name="expected">The expected net total.</param>
    /// <returns>A passing assertion when the total matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="instrumentName"/>
    /// is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasCounterTotal(this MeterCapture value, string instrumentName, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(instrumentName);
        var actual = value.CounterTotal(instrumentName);
        return actual == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have counter ", instrumentName, " total ",
                expected.ToString(CultureInfo.InvariantCulture),
                "\n  but it was ", actual.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the net value of a bundled up-down counter equals <paramref name="expected"/>.</summary>
    /// <param name="value">The meter capture.</param>
    /// <param name="instrumentName">The up-down-counter instrument name.</param>
    /// <param name="expected">The expected net value.</param>
    /// <returns>A passing assertion when the value matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="instrumentName"/>
    /// is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasUpDownCounterValue(this MeterCapture value, string instrumentName, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(instrumentName);
        var actual = value.CounterTotal(instrumentName);
        return actual == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have up-down counter ", instrumentName, " value ",
                expected.ToString(CultureInfo.InvariantCulture),
                "\n  but it was ", actual.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the measurement count for a bundled instrument equals <paramref name="expected"/>.</summary>
    /// <param name="value">The meter capture.</param>
    /// <param name="instrumentName">The instrument name.</param>
    /// <param name="expected">The expected measurement count.</param>
    /// <returns>A passing assertion when the count matches; otherwise a failing one naming both counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="instrumentName"/>
    /// is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasMeasurementCount(this MeterCapture value, string instrumentName, int expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(instrumentName);
        var actual = value.MeasurementCount(instrumentName);
        return actual == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have ", expected.ToString(CultureInfo.InvariantCulture),
                " measurement(s) for ", instrumentName,
                "\n  but it had ", actual.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts a bundled instrument has at least one measurement tagged <paramref name="tagKey"/>
    /// equal to <paramref name="tagValue"/>.</summary>
    /// <param name="value">The meter capture.</param>
    /// <param name="instrumentName">The instrument name.</param>
    /// <param name="tagKey">The tag key to match.</param>
    /// <param name="tagValue">The tag value to match.</param>
    /// <returns>A passing assertion when a matching measurement exists; otherwise a failing one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/>, <paramref name="instrumentName"/>,
    /// or <paramref name="tagKey"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasTaggedMeasurement(this MeterCapture value, string instrumentName, string tagKey, object? tagValue)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(instrumentName);
        ArgumentNullException.ThrowIfNull(tagKey);
        return value.HasMeasurementTagged(instrumentName, tagKey, tagValue)
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to have a measurement for ", instrumentName, " tagged ", tagKey, "=",
                Convert.ToString(tagValue, CultureInfo.InvariantCulture), "\n  but none matched"));
    }
}
