// What the README samples use without declaring it themselves: the placeholders of a test and a stand-in for the reader's
// own type. Compiled by CI together with every C# sample of the READMEs (readme_samples.py of assertions-ci); never part of
// a package.
using System;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using MetricsAssertions;

namespace ReadmeSamples;

/// <summary>The reader's own code under test: it emits to the meter it receives.</summary>
/// <param name="meter">The meter the service emits to.</param>
/// <param name="clock">The clock the service reads.</param>
public sealed class OrderService(Meter meter, TimeProvider clock)
{
    /// <summary>Gets the meter the service emits to.</summary>
    public Meter Meter { get; } = meter;

    /// <summary>Gets the clock the service reads.</summary>
    public TimeProvider Clock { get; } = clock;

    /// <summary>Places an order.</summary>
    /// <param name="id">The order id.</param>
    /// <returns>A task that completes when the order is placed.</returns>
    public Task PlaceAsync(string id) => Task.FromResult(string.Concat(Meter.Name, id, Clock.GetUtcNow()));
}

/// <summary>The members every README sample can use.</summary>
public partial class Prelude
{
    /// <summary>The capture of the test.</summary>
    protected InstrumentCapture capture { get; set; } = null!;

    /// <summary>The service under test.</summary>
    protected OrderService orders { get; set; } = null!;

    /// <summary>The address a client under test calls.</summary>
    protected Uri url { get; set; } = new("http://localhost/");
}
