using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace MetricsAssertions;

/// <summary>
/// A single-instrument measurement capture built on the first-party <see cref="MetricCollector{T}"/>
/// primitive (strongly typed, with an injectable <see cref="TimeProvider"/>). Captured measurements are
/// projected to a uniform <see cref="CapturedMeasurement"/> shape so heterogeneous instrument value types
/// share one assertion vocabulary.
/// </summary>
/// <remarks>
/// No OpenTelemetry SDK or exporter pipeline is involved: capture is the BCL testing primitive over an
/// <see cref="Instrument{T}"/>, so the type is AOT-safe. Create one per test with a
/// <see langword="using"/> statement for isolation; disposing releases the underlying collector. The
/// foundation release (v0.0.1) ships referenceable-instrument capture via <see cref="Of{T}"/> and the
/// <see cref="Measurements"/> snapshot; observable / by-name capture, tag and delta queries, async waiting,
/// and meter-wide bundling land in 0.1.0.
/// </remarks>
public sealed class InstrumentCapture : IDisposable
{
    private readonly IDisposable _collector;
    private readonly Func<IReadOnlyList<CapturedMeasurement>> _snapshot;

    /// <summary>Gets the name of the captured instrument.</summary>
    public string InstrumentName { get; }

    private InstrumentCapture(
        string instrumentName,
        IDisposable collector,
        Func<IReadOnlyList<CapturedMeasurement>> snapshot)
    {
        InstrumentName = instrumentName;
        _collector = collector;
        _snapshot = snapshot;
    }

    /// <summary>Captures the supplied (referenceable) instrument.</summary>
    /// <typeparam name="T">The instrument's value type.</typeparam>
    /// <param name="instrument">The instrument to capture.</param>
    /// <param name="timeProvider">An optional clock for measurement timestamps; the system clock when omitted.</param>
    /// <returns>A disposable capture collecting every measurement the instrument records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instrument"/> is <see langword="null"/>.</exception>
    public static InstrumentCapture Of<T>(Instrument<T> instrument, TimeProvider? timeProvider = null) where T : struct
    {
        ArgumentNullException.ThrowIfNull(instrument);

        var collector = new MetricCollector<T>(instrument, timeProvider);
        var name = instrument.Name;
        return new InstrumentCapture(name, collector, () =>
            [.. collector.GetMeasurementSnapshot().Select(m =>
                new CapturedMeasurement(
                    name,
                    Convert.ToDouble(m.Value, CultureInfo.InvariantCulture),
                    m.Tags,
                    m.Timestamp))]);
    }

    /// <summary>Gets a snapshot of every measurement captured so far.</summary>
    public IReadOnlyList<CapturedMeasurement> Measurements => _snapshot();

    /// <summary>Gets how many measurements were captured.</summary>
    public int Count => _snapshot().Count;

    /// <summary>Releases the underlying collector and stops capturing.</summary>
    public void Dispose() => _collector.Dispose();
}
