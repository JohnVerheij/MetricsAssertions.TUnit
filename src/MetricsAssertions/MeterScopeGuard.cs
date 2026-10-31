using System;
using System.Diagnostics.Metrics;

namespace MetricsAssertions;

/// <summary>
/// Rejects values that are never a <see cref="Meter.Scope"/> but fit the <see cref="object"/>-typed scope
/// parameter. A clock or a meter passed there would make the capture match no meter, and the empty result
/// would read as "the code emitted nothing".
/// </summary>
internal static class MeterScopeGuard
{
    /// <summary>Throws when <paramref name="meterScope"/> is a <see cref="TimeProvider"/> or a <see cref="Meter"/>.</summary>
    /// <param name="meterScope">The value passed as the meter scope.</param>
    /// <param name="parameterName">The name of the scope parameter, for the exception.</param>
    /// <exception cref="ArgumentException"><paramref name="meterScope"/> is a <see cref="TimeProvider"/> or a
    /// <see cref="Meter"/>.</exception>
    internal static void ThrowIfNotAScope(object? meterScope, string parameterName)
    {
        switch (meterScope)
        {
            case TimeProvider:
                throw new ArgumentException(
                    "A TimeProvider was passed as the meter scope. The scope is the IMeterFactory that created the meter (or null); pass the clock to the overload that takes a TimeProvider.",
                    parameterName);
            case Meter:
                throw new ArgumentException(
                    "A Meter was passed as the meter scope. Pass the Meter itself to the overload that takes a Meter, which captures that meter instance.",
                    parameterName);
        }
    }
}
