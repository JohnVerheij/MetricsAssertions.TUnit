using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace MetricsAssertions;

/// <summary>
/// A single-instrument measurement capture, built on the first-party <see cref="MetricCollector{T}"/>
/// primitive (strongly typed, with built-in async waiting and an injectable <see cref="TimeProvider"/>).
/// The generic value type is erased behind a uniform <see cref="MeasurementSet"/> surface so heterogeneous
/// instruments share one assertion vocabulary and bundle into a <see cref="MeterCapture"/>.
/// <para>
/// Construct via <see cref="Of{T}(Instrument{T}, TimeProvider?)"/> (when the instrument is referenceable),
/// <see cref="OfObservable{T}"/>, or <see cref="OfName{T}"/> (by meter + instrument name - the primitive
/// attaches even to a plain static <see cref="Meter"/>). For observable instruments, call
/// <see cref="RecordObservable"/> before reading. Dispose to release.
/// </para>
/// </summary>
public sealed class InstrumentCapture : IDisposable
{
    private readonly IDisposable _collector;
    private readonly Func<IReadOnlyList<CapturedMeasurement>> _rawSnapshot;
    private readonly Action _recordObservable;
    private readonly Func<int, CancellationToken, Task> _wait;
    private readonly object _token = new();

    /// <summary>Gets the name of the captured instrument.</summary>
    public string InstrumentName { get; }

    private InstrumentCapture(
        string instrumentName,
        IDisposable collector,
        Func<IReadOnlyList<CapturedMeasurement>> rawSnapshot,
        Action recordObservable,
        Func<int, CancellationToken, Task> wait)
    {
        InstrumentName = instrumentName;
        _collector = collector;
        _rawSnapshot = rawSnapshot;
        _recordObservable = recordObservable;
        _wait = wait;
    }

    /// <summary>Captures the supplied (referenceable) instrument.</summary>
    /// <typeparam name="T">The instrument's value type.</typeparam>
    /// <param name="instrument">The instrument to capture.</param>
    /// <param name="timeProvider">An optional clock for measurement timestamps and waits.</param>
    public static InstrumentCapture Of<T>(Instrument<T> instrument, TimeProvider? timeProvider = null) where T : struct
    {
        ArgumentNullException.ThrowIfNull(instrument);
        return Wrap(instrument.Name, new MetricCollector<T>(instrument, timeProvider));
    }

    /// <summary>Captures the supplied (referenceable) observable instrument.</summary>
    /// <typeparam name="T">The instrument's value type.</typeparam>
    /// <param name="instrument">The observable instrument to capture.</param>
    /// <param name="timeProvider">An optional clock for measurement timestamps and waits.</param>
    public static InstrumentCapture OfObservable<T>(ObservableInstrument<T> instrument, TimeProvider? timeProvider = null) where T : struct
    {
        ArgumentNullException.ThrowIfNull(instrument);
        return Wrap(instrument.Name, new MetricCollector<T>(instrument, timeProvider));
    }

    /// <summary>
    /// Captures the instrument named <paramref name="instrumentName"/> on the meter named
    /// <paramref name="meterName"/> (no instrument reference required).
    /// </summary>
    /// <typeparam name="T">The instrument's value type.</typeparam>
    /// <param name="meterName">The meter name.</param>
    /// <param name="instrumentName">The instrument name.</param>
    /// <param name="timeProvider">An optional clock for measurement timestamps and waits.</param>
    public static InstrumentCapture OfName<T>(string meterName, string instrumentName, TimeProvider? timeProvider = null) where T : struct
        => Wrap(instrumentName, new MetricCollector<T>(meterScope: null, meterName, instrumentName, timeProvider));

    private static InstrumentCapture Wrap<T>(string name, MetricCollector<T> collector) where T : struct
        => new(
            name,
            collector,
            rawSnapshot: () => [.. collector.GetMeasurementSnapshot().Select(m =>
                new CapturedMeasurement(name, Convert.ToDouble(m.Value, CultureInfo.InvariantCulture), m.Tags, m.Timestamp))],
            recordObservable: collector.RecordObservableInstruments,
            wait: collector.WaitForMeasurementsAsync);

    /// <summary>Gets a snapshot of every captured measurement as a queryable, assertable set.</summary>
    public MeasurementSet Measurements => new(_rawSnapshot());

    /// <summary>Returns the subset of captured measurements carrying a tag <paramref name="key"/>=<paramref name="value"/>.</summary>
    /// <param name="key">The tag key to match.</param>
    /// <param name="value">The tag value to match.</param>
    public MeasurementSet Tagged(string key, object? value) => Measurements.Tagged(key, value);

    /// <summary>Gets the net total of all captured values (the running total of a counter / up-down counter).</summary>
    public long Total => Measurements.Total;

    /// <summary>Gets how many measurements were captured.</summary>
    public int Count => _rawSnapshot().Count;

    /// <summary>Gets the most recent captured value, or <see langword="null"/> when none were captured.</summary>
    public double? LastValue => Measurements.LastValue;

    /// <summary>Takes a baseline at the current point in the stream for a later <see cref="Since"/> delta.</summary>
    public MeasurementBaseline Snapshot() => new(_rawSnapshot().Count, _token);

    /// <summary>Returns only the measurements captured after <paramref name="baseline"/> was taken.</summary>
    /// <param name="baseline">A baseline previously returned by this capture's <see cref="Snapshot"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="baseline"/> was taken from a different capture.</exception>
    public MeasurementSet Since(MeasurementBaseline baseline)
    {
        if (!ReferenceEquals(baseline.Owner, _token))
            throw new ArgumentException("The baseline was taken from a different InstrumentCapture.", nameof(baseline));
        return new(_rawSnapshot().Skip(baseline.Count));
    }

    /// <summary>Pulls the current values of the instrument when it is observable (a gauge).</summary>
    public void RecordObservable() => _recordObservable();

    /// <summary>Waits until at least <paramref name="count"/> measurements have been captured.</summary>
    /// <param name="count">The minimum number of measurements to wait for.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    public Task WaitForAsync(int count, CancellationToken cancellationToken = default) => _wait(count, cancellationToken);

    /// <summary>Returns whether at least one captured measurement carries a tag <paramref name="key"/>=<paramref name="value"/>.</summary>
    /// <param name="key">The tag key to match.</param>
    /// <param name="value">The tag value to match.</param>
    public bool HasMeasurementTagged(string key, object? value) => Tagged(key, value).Count > 0;

    /// <summary>Releases the underlying collector.</summary>
    public void Dispose() => _collector.Dispose();
}
