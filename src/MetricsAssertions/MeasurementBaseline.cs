namespace MetricsAssertions;

/// <summary>
/// An opaque marker for a point in an <see cref="InstrumentCapture"/>'s measurement stream, taken with
/// <see cref="InstrumentCapture.Snapshot"/> and passed to <see cref="InstrumentCapture.Since"/> to assert
/// only the measurements recorded after it (the delta produced by an action, independent of whatever a
/// long-lived static meter accumulated before).
/// </summary>
/// <param name="Count">The number of measurements captured at the moment the baseline was taken.</param>
public readonly record struct MeasurementBaseline(int Count);
