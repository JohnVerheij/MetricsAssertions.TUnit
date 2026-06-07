namespace MetricsAssertions;

/// <summary>
/// An opaque marker for a point in an <see cref="InstrumentCapture"/>'s measurement stream, taken with
/// <see cref="InstrumentCapture.Snapshot"/> and passed to <see cref="InstrumentCapture.Since"/> to assert
/// only the measurements recorded after it (the delta produced by an action, independent of whatever a
/// long-lived static meter accumulated before). A baseline is bound to the capture that produced it;
/// passing it to a different capture's <see cref="InstrumentCapture.Since"/> throws.
/// </summary>
public readonly record struct MeasurementBaseline
{
    internal MeasurementBaseline(int count, object owner)
    {
        Count = count;
        Owner = owner;
    }

    /// <summary>Gets the number of measurements captured at the moment the baseline was taken.</summary>
    public int Count { get; }

    /// <summary>Gets the token identifying the capture that produced this baseline.</summary>
    internal object Owner { get; }
}
