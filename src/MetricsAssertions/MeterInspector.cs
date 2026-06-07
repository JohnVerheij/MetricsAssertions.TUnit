using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace MetricsAssertions;

/// <summary>
/// Discovery helper for the one thing <see cref="MetricCollector{T}"/> cannot answer: which instruments a
/// meter actually publishes (a registry question, not a measurement one). Uses a short-lived
/// <see cref="MeterListener"/> - the only mechanism that enumerates a meter's instruments without knowing
/// their names or value types up front.
/// </summary>
public static class MeterInspector
{
    /// <summary>Returns the names of every instrument currently published by the named meter.</summary>
    /// <param name="meterName">The meter name to inspect.</param>
    public static IReadOnlySet<string> PublishedInstrumentNames(string meterName)
    {
        ArgumentNullException.ThrowIfNull(meterName);

        var names = new HashSet<string>(StringComparer.Ordinal);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
                    names.Add(instrument.Name);
            },
        };
        listener.Start();
        return names;
    }

    /// <summary>Returns whether the named instrument is published by the named meter.</summary>
    /// <param name="meterName">The meter name.</param>
    /// <param name="instrumentName">The instrument name.</param>
    public static bool IsPublished(string meterName, string instrumentName)
        => PublishedInstrumentNames(meterName).Contains(instrumentName);

    /// <summary>Returns whether the named meter publishes every one of <paramref name="instrumentNames"/>.</summary>
    /// <param name="meterName">The meter name.</param>
    /// <param name="instrumentNames">The instrument names that must all be published.</param>
    public static bool PublishesAll(string meterName, params string[] instrumentNames)
    {
        ArgumentNullException.ThrowIfNull(instrumentNames);
        IReadOnlySet<string> published = PublishedInstrumentNames(meterName);
        return Array.TrueForAll(instrumentNames, published.Contains);
    }
}
