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
/// Build with <c>For</c> + <c>Add*</c>; query per instrument via the indexer or
/// <see cref="CounterTotal"/> / <see cref="Samples"/> / <see cref="MeasurementCount"/>, or across all via
/// <see cref="Measurements"/>. Call <see cref="RecordObservable"/> before reading observable gauges. A clock
/// passed to <c>For</c> applies to every bundled instrument.
/// </para>
/// <para>
/// Capture by meter name is process-wide: each bundled instrument attaches to the first instrument in the
/// process with that meter name, scope and instrument name, and records every measurement that instrument receives,
/// whichever test or thread emits it. With a static meter, that is the instrument every test emits to, so the
/// measurements of a test running in parallel land in this capture too. A keyed <c>[NotInParallel]</c> does not
/// prevent that: it only serializes the tests that carry the same key, and any other test that emits to the meter
/// keeps running. With a meter created per test, the capture can attach to another test's instrument and miss
/// this test's measurements.
/// </para>
/// <para>
/// For isolation, scope the capture to the async flow of the test with <see cref="InCurrentFlow"/> (the first remedy for a
/// static meter), capture the <see cref="Meter"/> instance itself with <see cref="For(Meter)"/>, create the
/// meter through a per-test <see cref="IMeterFactory"/> and pass it as the scope, or serialize every test that emits to
/// the meter with an un-keyed <c>[NotInParallel]</c>.
/// </para>
/// </summary>
public sealed class MeterCapture : IDisposable
{
    private readonly Dictionary<string, InstrumentCapture> _instruments = new(StringComparer.Ordinal);
    private readonly object? _meterScope;
    private readonly Meter? _meter;
    private readonly TimeProvider? _timeProvider;
    private object? _flowToken;

    /// <summary>Gets the meter name this capture is scoped to.</summary>
    public string MeterName { get; }

    private MeterCapture(string meterName, object? meterScope, Meter? meter, TimeProvider? timeProvider)
    {
        MeterName = meterName;
        _meterScope = meterScope;
        _meter = meter;
        _timeProvider = timeProvider;
    }

    /// <summary>Begins building a capture for the meter named <paramref name="meterName"/> created
    /// directly (<c>new Meter(name)</c>, no scope), on the system clock. For a meter created by an
    /// <see cref="IMeterFactory"/>, use <see cref="For(string, object?)"/>.</summary>
    /// <param name="meterName">The meter name to capture.</param>
    public static MeterCapture For(string meterName)
        => Create(meterName, meterScope: null, timeProvider: null);

    /// <summary>Begins building a capture for the meter named <paramref name="meterName"/> created
    /// directly (<c>new Meter(name)</c>, no scope), with <paramref name="timeProvider"/> as the clock for
    /// every instrument added to it.</summary>
    /// <param name="meterName">The meter name to capture.</param>
    /// <param name="timeProvider">The clock for the measurement timestamps of every bundled instrument,
    /// or <see langword="null"/> for the system clock.</param>
    public static MeterCapture For(string meterName, TimeProvider? timeProvider)
        => Create(meterName, meterScope: null, timeProvider);

    /// <summary>Begins building a capture for the meter named <paramref name="meterName"/> whose
    /// <see cref="Meter.Scope"/> equals <paramref name="meterScope"/>, on the system clock. Pass the
    /// <see cref="IMeterFactory"/> instance for a DI-created meter; a null scope only matches a
    /// directly-created meter and would capture nothing from a factory-created one.</summary>
    /// <param name="meterName">The meter name to capture.</param>
    /// <param name="meterScope">The meter scope to match (the <c>IMeterFactory</c> instance, or null).</param>
    /// <exception cref="ArgumentException"><paramref name="meterScope"/> is a <see cref="TimeProvider"/>
    /// or a <see cref="Meter"/>, almost always passed there by mistake. Capture a meter whose scope really is one
    /// of these by instance with <see cref="For(Meter)"/>.</exception>
    public static MeterCapture For(string meterName, object? meterScope)
        => Create(meterName, meterScope, timeProvider: null);

    /// <summary>Begins building a capture for the meter named <paramref name="meterName"/> whose
    /// <see cref="Meter.Scope"/> equals <paramref name="meterScope"/>, with
    /// <paramref name="timeProvider"/> as the clock for every instrument added to it.</summary>
    /// <param name="meterName">The meter name to capture.</param>
    /// <param name="meterScope">The meter scope to match (the <c>IMeterFactory</c> instance, or null).</param>
    /// <param name="timeProvider">The clock for the measurement timestamps of every bundled instrument,
    /// or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentException"><paramref name="meterScope"/> is a <see cref="TimeProvider"/>
    /// or a <see cref="Meter"/>, almost always passed there by mistake. Capture a meter whose scope really is one
    /// of these by instance with <see cref="For(Meter)"/>.</exception>
    public static MeterCapture For(string meterName, object? meterScope, TimeProvider? timeProvider)
        => Create(meterName, meterScope, timeProvider);

    /// <summary>Begins building a capture for the instruments of <paramref name="meter"/> itself, on the
    /// system clock. Unlike capture by name, another meter with the same name and scope (for example one
    /// created by a test running in parallel) is not captured.</summary>
    /// <param name="meter">The meter instance to capture.</param>
    public static MeterCapture For(Meter meter) => For(meter, timeProvider: null);

    /// <summary>Begins building a capture for the instruments of <paramref name="meter"/> itself, with
    /// <paramref name="timeProvider"/> as the clock for every instrument added to it. Unlike capture by
    /// name, another meter with the same name and scope is not captured.</summary>
    /// <param name="meter">The meter instance to capture.</param>
    /// <param name="timeProvider">The clock for the measurement timestamps of every bundled instrument,
    /// or <see langword="null"/> for the system clock.</param>
    public static MeterCapture For(Meter meter, TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(meter);
        return new MeterCapture(meter.Name, meter.Scope, meter, timeProvider);
    }

    private static MeterCapture Create(string meterName, object? meterScope, TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(meterName);
        MeterScopeGuard.ThrowIfNotAScope(meterScope, nameof(meterScope));
        return new MeterCapture(meterName, meterScope, meter: null, timeProvider);
    }

    /// <summary>Adds the named instrument (typically a counter / up-down counter / histogram) to the bundle,
    /// on the capture's clock.</summary>
    /// <typeparam name="T">The instrument's value type.</typeparam>
    /// <param name="instrumentName">The instrument name.</param>
    public MeterCapture Add<T>(string instrumentName) where T : struct
    {
        ArgumentNullException.ThrowIfNull(instrumentName);
        if (_flowToken is not null)
        {
            Replace(instrumentName, InstrumentCapture.InFlow<T>(_flowToken, instrumentName, MatchesNamedInstrument(instrumentName), _timeProvider));
            return this;
        }

        Replace(instrumentName, _meter is null
            ? InstrumentCapture.OfName<T>(_meterScope, MeterName, instrumentName, _timeProvider)
            : InstrumentCapture.OfName<T>(_meter, instrumentName, _timeProvider));
        return this;
    }

    /// <summary>Adds the referenceable instrument to the bundle, on the capture's clock.</summary>
    /// <typeparam name="T">The instrument's value type.</typeparam>
    /// <param name="instrument">The instrument to capture.</param>
    /// <exception cref="ArgumentException"><paramref name="instrument"/> belongs to a different meter than the
    /// captured one.</exception>
    public MeterCapture Add<T>(Instrument<T> instrument) where T : struct
    {
        ArgumentNullException.ThrowIfNull(instrument);
        if (!IsCapturedMeter(instrument.Meter))
            throw new ArgumentException(
                _meter is null
                    ? $"Instrument '{instrument.Name}' belongs to meter '{instrument.Meter.Name}', not the captured meter '{MeterName}' with the matching scope; the meter name and scope must both match."
                    : $"Instrument '{instrument.Name}' belongs to a different Meter instance than the captured meter '{MeterName}'; a capture created from a Meter only accepts instruments of that instance.",
                nameof(instrument));
        Replace(instrument.Name, _flowToken is null
            ? InstrumentCapture.Of(instrument, _timeProvider)
            : InstrumentCapture.InFlow<T>(_flowToken, instrument.Name, i => ReferenceEquals(i, instrument), _timeProvider));
        return this;
    }

    /// <summary>
    /// Makes this capture record only the measurements emitted in the async flow of the code that calls this method:
    /// that flow, and the flows it starts afterwards (<see langword="await"/> continuations, <c>Task.Run</c>,
    /// <c>Parallel.ForEachAsync</c>, <c>ThreadPool.QueueUserWorkItem</c>, timers). A measurement of a test running in
    /// parallel is not recorded, even from the same static meter. Call it right after <c>For</c>, before the first
    /// <c>Add</c>. Every instrument added afterwards attaches to every matching instrument in the process, not the first
    /// one only, so a meter created per test is not missed.
    /// <para>
    /// What the flow rule does not cover: a capture created inside an awaited async method does not reach the caller
    /// (the marker does not flow back up), so create it in the test method or in a synchronous helper; measurements
    /// emitted by a thread started with <c>ExecutionContext.SuppressFlow()</c>, or by a service or thread that was already
    /// running before this call, are not recorded. When such an emission is the one under test, use capture by
    /// <see cref="For(Meter)"/> or a serialized test instead. If a flow-scoped capture is empty while other flows emitted
    /// to the same instruments, <see cref="MeasurementSet.Describe"/> (and so every failure message) says so.
    /// </para>
    /// </summary>
    /// <returns>This capture.</returns>
    /// <exception cref="InvalidOperationException">The capture is already flow-scoped, or an instrument was already added.</exception>
    public MeterCapture InCurrentFlow()
    {
        if (_flowToken is not null)
            throw new InvalidOperationException("The capture is already scoped to the current flow.");
        if (_instruments.Count > 0)
            throw new InvalidOperationException("InCurrentFlow must be called before the first Add: an instrument that was added earlier is not scoped to the flow.");
        _flowToken = FlowContext.Enter();
        return this;
    }

    private Func<Instrument, bool> MatchesNamedInstrument(string instrumentName)
        => instrument => string.Equals(instrument.Name, instrumentName, StringComparison.Ordinal) && IsCapturedMeter(instrument.Meter);

    private bool IsCapturedMeter(Meter meter)
        => _meter is null
            ? string.Equals(meter.Name, MeterName, StringComparison.Ordinal) && Equals(meter.Scope, _meterScope)
            : ReferenceEquals(meter, _meter);

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
    public MeasurementSet Measurements
        => new(_instruments.Values.SelectMany(static i => i.Measurements.All), _instruments.Values.Sum(static i => i.IgnoredFromOtherFlows));

    /// <summary>Pulls the current values of all bundled observable instruments (gauges).</summary>
    public void RecordObservable()
    {
        foreach (InstrumentCapture instrument in _instruments.Values)
            instrument.RecordObservable();
    }

    /// <summary>Gets the net total for a bundled counter / up-down counter.</summary>
    /// <param name="instrumentName">The instrument name.</param>
    /// <exception cref="OverflowException">The values sum to something no <see cref="long"/> can hold; see
    /// <see cref="MeasurementSet.Total"/>.</exception>
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
