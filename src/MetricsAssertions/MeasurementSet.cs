using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MetricsAssertions;

/// <summary>
/// An immutable, queryable set of captured measurements - the core unit assertions are written against.
/// Produced by an <see cref="InstrumentCapture"/> or <see cref="MeterCapture"/> and narrowed with
/// <see cref="Tagged(string, object?)"/> / <see cref="ForInstrument"/>. Exposes counter-style aggregates
/// (<see cref="Total"/>), histogram-style aggregates (<see cref="Sum"/>/<see cref="Min"/>/<see cref="Max"/>/
/// <see cref="Average"/>), the raw <see cref="Values"/> and <see cref="All"/> measurements, and a
/// deterministic <see cref="ToSnapshotString"/> projection for snapshot-style baselines.
/// </summary>
public sealed class MeasurementSet
{
    /// <summary>Creates a set over the supplied measurements (defensively copied).</summary>
    /// <param name="measurements">The measurements in the set.</param>
    public MeasurementSet(IEnumerable<CapturedMeasurement> measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);
        All = [.. measurements];
    }

    /// <summary>An empty set.</summary>
    public static MeasurementSet Empty { get; } = new([]);

    /// <summary>Gets every measurement in the set, in capture order.</summary>
    public IReadOnlyList<CapturedMeasurement> All { get; }

    /// <summary>Gets the number of measurements in the set.</summary>
    public int Count => All.Count;

    /// <summary>Gets whether the set contains no measurements.</summary>
    public bool IsEmpty => All.Count is 0;

    /// <summary>Gets the individual measured values, in capture order.</summary>
    public IReadOnlyList<double> Values => [.. All.Select(m => m.Value)];

    /// <summary>Gets the net total of all values as a <see cref="long"/> (a counter / up-down-counter total).</summary>
    public long Total => Convert.ToInt64(Sum);

    /// <summary>Gets the sum of all values (0 when empty).</summary>
    public double Sum => All.Sum(m => m.Value);

    /// <summary>Gets the smallest value (0 when empty).</summary>
    public double Min => All.Count is 0 ? 0d : All.Min(m => m.Value);

    /// <summary>Gets the largest value (0 when empty).</summary>
    public double Max => All.Count is 0 ? 0d : All.Max(m => m.Value);

    /// <summary>Gets the mean of all values (0 when empty).</summary>
    public double Average => All.Count is 0 ? 0d : All.Average(m => m.Value);

    /// <summary>Gets the most recently captured value, or <see langword="null"/> when the set is empty.</summary>
    public double? LastValue => All.Count is 0 ? null : All[^1].Value;

    /// <summary>Returns the subset whose measurements carry a tag <paramref name="key"/> equal to <paramref name="value"/>.</summary>
    /// <param name="key">The tag key to match.</param>
    /// <param name="value">The tag value to match (string-compared invariantly).</param>
    public MeasurementSet Tagged(string key, object? value)
        => new(All.Where(m => TagEquals(m, key, value)));

    /// <summary>Returns the subset whose measurements carry all of the supplied tags.</summary>
    /// <param name="tags">The tags that must all be present and equal.</param>
    public MeasurementSet Tagged(params (string Key, object? Value)[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return new(All.Where(m => Array.TrueForAll(tags, t => TagEquals(m, t.Key, t.Value))));
    }

    /// <summary>Returns whether the values equal <paramref name="expected"/> in order.</summary>
    /// <param name="expected">The expected samples, in order.</param>
    public bool SamplesEqual(params double[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return Values.SequenceEqual(expected);
    }

    /// <summary>Returns whether the values equal <paramref name="expected"/> regardless of order.</summary>
    /// <param name="expected">The expected samples, in any order.</param>
    public bool SamplesEquivalentTo(params double[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return Values.Order().SequenceEqual(expected.Order());
    }

    /// <summary>Returns the subset emitted by the named instrument.</summary>
    /// <param name="instrumentName">The instrument name to filter by.</param>
    public MeasurementSet ForInstrument(string instrumentName)
        => new(All.Where(m => string.Equals(m.InstrumentName, instrumentName, StringComparison.Ordinal)));

    /// <summary>Returns whether every measurement in the set carries a tag with the given <paramref name="key"/>.</summary>
    /// <param name="key">The tag key that must be present on every measurement.</param>
    public bool EveryMeasurementCarriesTag(string key)
        => All.Count > 0 && All.All(m => m.Tags.ContainsKey(key));

    /// <summary>Returns whether all values lie within the inclusive range [<paramref name="min"/>, <paramref name="max"/>].</summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public bool AllValuesInRange(double min, double max)
        => All.All(m => m.Value >= min && m.Value <= max);

    /// <summary>
    /// Returns a deterministic, human-readable projection of the set (one line per measurement, sorted by
    /// instrument, value, then tags) suitable for snapshot-style baselines via a snapshot assertion library.
    /// </summary>
    public string ToSnapshotString()
    {
        var sb = new StringBuilder();
        foreach (CapturedMeasurement m in All
            .OrderBy(m => m.InstrumentName, StringComparer.Ordinal)
            .ThenBy(m => m.Value)
            .ThenBy(FormatTags, StringComparer.Ordinal))
        {
            sb.Append(m.InstrumentName).Append(' ')
                .Append(m.Value.ToString(CultureInfo.InvariantCulture));
            var tags = FormatTags(m);
            if (tags.Length > 0)
                sb.Append(" {").Append(tags).Append('}');
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static bool TagEquals(CapturedMeasurement measurement, string key, object? value)
        => measurement.Tags.TryGetValue(key, out var actual)
            && string.Equals(
                Convert.ToString(actual, CultureInfo.InvariantCulture),
                Convert.ToString(value, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);

    private static string FormatTags(CapturedMeasurement measurement)
        => string.Join(',', measurement.Tags
            .OrderBy(t => t.Key, StringComparer.Ordinal)
            .Select(t => $"{t.Key}={Convert.ToString(t.Value, CultureInfo.InvariantCulture)}"));
}
