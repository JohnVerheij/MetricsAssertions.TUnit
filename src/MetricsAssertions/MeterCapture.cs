using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace MetricsAssertions;

/// <summary>
/// A meter-wide measurement capture composed from a bundle of per-instrument
/// <see cref="InstrumentCapture"/> (each backed by a <see cref="MetricCollector{T}"/>). Built fluently
/// for a known instrument set - strongly typed at construction, uniform to query and assert - for tests
/// that need to assert across several instruments of one meter at once (e.g. an observable-gauge scrape).
/// <para>
/// Build with <see cref="For(string)"/> + <c>Add*</c>; query per instrument via the indexer or
/// <see cref="CounterTotal"/> / <see cref="Samples"/> / <see cref="MeasurementCount"/>, or across all via
/// <see cref="Measurements"/>. Call <see cref="RecordObservable"/> before reading observable gauges.
/// </para>
/// </summary>
public sealed class MeterCapture : IDisposable
{
    private readonly Dictionary<string, InstrumentCapture> _instruments = new(StringComparer.Ordinal);
    private readonly object? _meterScope;

    /// <summary>Gets the meter name this capture is scoped to.</summary>
    public string MeterName { get; }

    private MeterCapture(string meterName, object? meterScope)
    {
        MeterName = meterName;
        _meterScope = meterScope;
    }

    /// <summary>Begins building a capture for the meter named <paramref name="meterName"/> created
    /// directly (<c>new Meter(name)</c>, no scope). For a meter created by an
    /// <see cref="System.Diagnostics.Metrics.IMeterFactory"/>, use <see cref="For(string, object?)"/>.</summary>
    /// <param name="meterName">The meter name to capture.</param>
    public static MeterCapture For(string meterName)
        => For(meterName, meterScope: null);

    /// <summary>Begins building a capture for the meter named <paramref name="meterName"/> whose
    /// <see cref="System.Diagnostics.Metrics.Meter.Scope"/> equals <paramref name="meterScope"/>. Pass the
    /// <see cref="System.Diagnostics.Metrics.IMeterFactory"/> instance for a DI-created meter; a null scope
    /// only matches a directly-created meter and would capture nothing from a factory-created one.</summary>
    /// <param name="meterName">The meter name to capture.</param>
    /// <param name="meterScope">The meter scope to match (the <c>IMeterFactory</c> instance, or null).</param>
    public static MeterCapture For(string meterName, object? meterScope)
    {
        ArgumentNullException.ThrowIfNull(meterName);
        return new MeterCapture(meterName, meterScope);
    }

    /// <summary>Adds the named instrument (typically a counter / up-down counter / histogram) to the bundle.</summary>
    /// <typeparam name="T">The instrument's value type.</typeparam>
    /// <param name="instrumentName">The instrument name.</param>
    /// <param name="timeProvider">An optional clock for measurement timestamps and waits.</param>
    public MeterCapture Add<T>(string instrumentName, TimeProvider? timeProvider = null) where T : struct
    {
        ArgumentNullException.ThrowIfNull(instrumentName);
        Replace(instrumentName, InstrumentCapture.OfName<T>(_meterScope, MeterName, instrumentName, timeProvider));
        return this;
    }

    /// <summary>Adds the referenceable instrument to the bundle.</summary>
    /// <typeparam name="T">The instrument's value type.</typeparam>
    /// <param name="instrument">The instrument to capture.</param>
    /// <param name="timeProvider">An optional clock for measurement timestamps and waits.</param>
    public MeterCapture Add<T>(Instrument<T> instrument, TimeProvider? timeProvider = null) where T : struct
    {
        ArgumentNullException.ThrowIfNull(instrument);
        if (!string.Equals(instrument.Meter.Name, MeterName, StringComparison.Ordinal)
            || !Equals(instrument.Meter.Scope, _meterScope))
            throw new ArgumentException(
                $"Instrument '{instrument.Name}' belongs to meter '{instrument.Meter.Name}', not the captured meter '{MeterName}' with the matching scope; the meter name and scope must both match.",
                nameof(instrument));
        Replace(instrument.Name, InstrumentCapture.Of(instrument, timeProvider));
        return this;
    }

    /// <summary>Gets the per-instrument capture for <paramref name="instrumentName"/>.</summary>
    /// <param name="instrumentName">The instrument name added to the bundle.</param>
    public InstrumentCapture this[string instrumentName] => _instruments[instrumentName];

    /// <summary>Returns whether an instrument named <paramref name="instrumentName"/> is bundled.</summary>
    /// <param name="instrumentName">The instrument name to look for.</param>
    public bool Contains(string instrumentName)
    {
        ArgumentNullException.ThrowIfNull(instrumentName);
        return _instruments.ContainsKey(instrumentName);
    }

    /// <summary>Gets every captured measurement across all bundled instruments as a queryable, assertable set.</summary>
    public MeasurementSet Measurements => new(_instruments.Values.SelectMany(static i => i.Measurements.All));

    /// <summary>Pulls the current values of all bundled observable instruments (gauges).</summary>
    public void RecordObservable()
    {
        foreach (InstrumentCapture instrument in _instruments.Values)
            instrument.RecordObservable();
    }

    /// <summary>Gets the net total for a bundled counter / up-down counter.</summary>
    /// <param name="instrumentName">The instrument name.</param>
    public long CounterTotal(string instrumentName) => _instruments[instrumentName].Total;

    /// <summary>Gets the samples for a bundled histogram.</summary>
    /// <param name="instrumentName">The instrument name.</param>
    public IReadOnlyList<double> Samples(string instrumentName) => _instruments[instrumentName].Measurements.Values;

    /// <summary>Gets the measurement count for a bundled instrument.</summary>
    /// <param name="instrumentName">The instrument name.</param>
    public int MeasurementCount(string instrumentName) => _instruments[instrumentName].Count;

    /// <summary>Returns whether a bundled instrument has a measurement tagged <paramref name="tagKey"/>=<paramref name="tagValue"/>.</summary>
    /// <param name="instrumentName">The instrument name.</param>
    /// <param name="tagKey">The tag key to match.</param>
    /// <param name="tagValue">The tag value to match.</param>
    public bool HasMeasurementTagged(string instrumentName, string tagKey, object? tagValue)
        => _instruments[instrumentName].HasMeasurementTagged(tagKey, tagValue);

    /// <summary>Adds a capture under <paramref name="instrumentName"/>, disposing any capture it replaces.</summary>
    private void Replace(string instrumentName, InstrumentCapture capture)
    {
        if (_instruments.TryGetValue(instrumentName, out InstrumentCapture? existing))
            existing.Dispose();
        _instruments[instrumentName] = capture;
    }

    /// <summary>Releases every bundled collector.</summary>
    public void Dispose()
    {
        foreach (InstrumentCapture instrument in _instruments.Values)
            instrument.Dispose();
    }
}
