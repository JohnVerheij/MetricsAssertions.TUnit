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
    /// <remarks>A counter never decrements, so the assertion fails when a negative delta was recorded; use <c>HasUpDownCounterValue</c> for an up-down counter. It also fails, with a message, when the values sum to something no <see cref="long"/> can hold (not a number, infinite, or beyond the range of a long). A capture that recorded nothing has a total of 0, so a total of 0 passes on an empty capture; <see cref="MeterInspector"/> tells "nothing was emitted" from "the instrument does not exist".</remarks>
    /// <returns>A passing assertion when the total matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="instrumentName"/>
    /// is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have counter {instrumentName} total {expected}", InlineMethodBody = false)]
    public static AssertionResult HasCounterTotal(this MeterCapture value, string instrumentName, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(instrumentName);
        if (!value.Contains(instrumentName))
            return NotBundled(value, instrumentName);
        MeasurementSet set = value[instrumentName].Measurements;
        if (set.Min < 0)
            return MeasurementDiagnostics.Failed(set, string.Concat(
                MeasurementDiagnostics.EscapeLineBreaks(instrumentName), " recorded a negative delta, which a counter never does"));
        if (MeasurementDiagnostics.TotalOutOfRange(set) is { } outOfRange)
            return outOfRange;
        return set.Total == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(set, string.Concat("it was ", set.Total.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the net value of a bundled up-down counter equals <paramref name="expected"/>.</summary>
    /// <param name="value">The meter capture.</param>
    /// <param name="instrumentName">The up-down-counter instrument name.</param>
    /// <param name="expected">The expected net value.</param>
    /// <remarks>Unlike the counter assertions, negative deltas are part of the value. The assertion fails, with a message, when the values sum to something no <see cref="long"/> can hold.</remarks>
    /// <returns>A passing assertion when the value matches; otherwise a failing one naming both values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="instrumentName"/>
    /// is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have up-down counter {instrumentName} value {expected}", InlineMethodBody = false)]
    public static AssertionResult HasUpDownCounterValue(this MeterCapture value, string instrumentName, long expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(instrumentName);
        if (!value.Contains(instrumentName))
            return NotBundled(value, instrumentName);
        MeasurementSet all = value.Measurements;
        MeasurementSet set = all.ForInstrument(instrumentName);
        if (MeasurementDiagnostics.TotalOutOfRange(set) is { } outOfRange)
            return outOfRange;
        var actual = set.Total;
        return actual == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(all, string.Concat("it was ", actual.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts the measurement count for a bundled instrument equals <paramref name="expected"/>.</summary>
    /// <param name="value">The meter capture.</param>
    /// <param name="instrumentName">The instrument name.</param>
    /// <param name="expected">The expected measurement count.</param>
    /// <returns>A passing assertion when the count matches; otherwise a failing one naming both counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="instrumentName"/>
    /// is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have {expected} measurement(s) for {instrumentName}", InlineMethodBody = false)]
    public static AssertionResult HasMeasurementCount(this MeterCapture value, string instrumentName, int expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(instrumentName);
        if (!value.Contains(instrumentName))
            return NotBundled(value, instrumentName);
        MeasurementSet all = value.Measurements;
        var actual = all.ForInstrument(instrumentName).Count;
        return actual == expected
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(all, string.Concat("it had ", actual.ToString(CultureInfo.InvariantCulture)));
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
    [GenerateAssertion(ExpectationMessage = "to have a measurement for {instrumentName} tagged {tagKey}={tagValue}", InlineMethodBody = false)]
    public static AssertionResult HasTaggedMeasurement(this MeterCapture value, string instrumentName, string tagKey, object? tagValue)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(instrumentName);
        ArgumentNullException.ThrowIfNull(tagKey);
        if (!value.Contains(instrumentName))
            return NotBundled(value, instrumentName);
        MeasurementSet all = value.Measurements;
        return all.ForInstrument(instrumentName).Tagged(tagKey, tagValue).Count > 0
            ? AssertionResult.Passed
            : MeasurementDiagnostics.Failed(all, "none matched");
    }

    /// <summary>Builds the failure for an instrument name that was never added to the capture.</summary>
    /// <param name="capture">The capture the assertion ran on.</param>
    /// <param name="instrumentName">The instrument name that is not bundled.</param>
    /// <returns>A failing result naming the instrument and dumping what the capture holds.</returns>
    private static AssertionResult NotBundled(MeterCapture capture, string instrumentName)
        => MeasurementDiagnostics.Failed(capture.Measurements, string.Concat("no instrument named ", MeasurementDiagnostics.EscapeLineBreaks(instrumentName), " is bundled"));
}
