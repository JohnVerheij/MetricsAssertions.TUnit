using System;

namespace MetricsAssertions.TUnit.Tests;

/// <summary>A clock that always reports <paramref name="now"/>, so a failure message dump carries a known timestamp.</summary>
/// <param name="now">The instant <see cref="GetUtcNow"/> returns.</param>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>The instant every capture in the failure-message tests is stamped with.</summary>
    internal static readonly DateTimeOffset Instant = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
