using System;
using System.Collections.Generic;

namespace MetricsAssertions;

/// <summary>
/// A single captured metric measurement, normalized for assertions: the instrument it came from, its
/// numeric value (any instrument value type, projected to <see cref="double"/> for uniform querying),
/// the tags attached to it, and the time it was collected.
/// </summary>
/// <param name="InstrumentName">The name of the instrument that emitted the measurement.</param>
/// <param name="Value">The measured value, projected to <see cref="double"/>.</param>
/// <param name="Tags">The tags attached to the measurement (empty when none).</param>
/// <param name="Timestamp">The time the measurement was collected (from the capturing collector's clock).</param>
public sealed record CapturedMeasurement(
    string InstrumentName,
    double Value,
    IReadOnlyDictionary<string, object?> Tags,
    DateTimeOffset Timestamp);
