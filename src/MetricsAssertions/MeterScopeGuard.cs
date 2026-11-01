using System;
using System.Diagnostics.Metrics;

namespace MetricsAssertions;

/// <summary>
/// Rejects a <see cref="TimeProvider"/> or a <see cref="Meter"/> in the <see cref="object"/>-typed scope parameter.
/// Either fits the parameter, and either there is almost always a mistake (the clock or the meter itself passed where
/// the scope goes): the capture would match no meter, and the empty result would read as "the code emitted nothing".
/// <see cref="Meter.Scope"/> accepts any object, so a meter scoped by one of them is legal, but it has no known use;
/// such a meter is captured by instance (<see cref="MeterCapture.For(Meter)"/>,
/// <see cref="InstrumentCapture.OfName{T}(Meter, string, TimeProvider?)"/>), which matches the meter whatever its
/// scope.
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
