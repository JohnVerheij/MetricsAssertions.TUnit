using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace MetricsAssertions.Tests;

/// <summary>
/// A per-test <see cref="IMeterFactory"/> that, as the factory of <c>services.AddMetrics()</c> does, makes itself the scope
/// of every meter it creates, so a capture with this factory as the meter scope sees the meters of this factory only.
/// </summary>
internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];
    private readonly System.Threading.Lock _gate = new();

    /// <inheritdoc />
    public Meter Create(MeterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Scope = this;
        var meter = new Meter(options);
        lock (_gate)
            _meters.Add(meter);
        return meter;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (Meter meter in _meters)
                meter.Dispose();
            _meters.Clear();
        }
    }
}
