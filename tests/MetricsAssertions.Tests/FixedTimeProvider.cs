using System;

namespace MetricsAssertions.Tests;

/// <summary>A clock that always reports <paramref name="now"/>, so a test can tell which clock stamped a measurement.</summary>
/// <param name="now">The instant <see cref="GetUtcNow"/> returns.</param>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>Gets the fixed instant.</summary>
    public DateTimeOffset Now { get; } = now;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Now;
}
