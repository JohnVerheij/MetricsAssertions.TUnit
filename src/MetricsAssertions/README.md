# MetricsAssertions

[![NuGet](https://img.shields.io/nuget/v/MetricsAssertions.svg)](https://www.nuget.org/packages/MetricsAssertions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

Framework-agnostic core for fluent metric-measurement assertions over `System.Diagnostics.Metrics` instruments, built on the first-party `MetricCollector` testing primitive. Test-framework assertion entry points live in adapter packages; **[MetricsAssertions.TUnit](https://www.nuget.org/packages/MetricsAssertions.TUnit/)** ships today.

## What ships

- `InstrumentCapture.Of<T>(Instrument<T>, TimeProvider?)` captures the measurements a referenceable instrument records, via `MetricCollector<T>`.
- `InstrumentCapture.Measurements` (projected snapshot) and `Count`.
- `CapturedMeasurement` (instrument name, value projected to `double`, tags, timestamp).

Meter-wide capture, observable and by-name capture, totals, and tag and delta queries land in 0.1.0.

## Install

```bash
# Most consumers install a test-framework adapter, which pulls this in transitively:
dotnet add package MetricsAssertions.TUnit
```

The full reference is in the [GitHub README](https://github.com/JohnVerheij/MetricsAssertions.TUnit#readme).

## License

MIT.
