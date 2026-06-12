# MetricsAssertions

[![NuGet](https://img.shields.io/nuget/v/MetricsAssertions.svg)](https://www.nuget.org/packages/MetricsAssertions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

Framework-agnostic core for fluent metric-measurement assertions over `System.Diagnostics.Metrics` instruments, built on the first-party `MetricCollector` testing primitive. Test-framework assertion entry points live in adapter packages; **[MetricsAssertions.TUnit](https://www.nuget.org/packages/MetricsAssertions.TUnit/)** ships today.

## What ships

- `InstrumentCapture` captures a referenceable, observable, or by-name instrument (`Of` / `OfObservable` / `OfName`) via `MetricCollector<T>`, with totals, last value, tag queries, baseline deltas (`Snapshot` / `Since`), and `WaitForAsync`. The by-name captures (`OfName`, and `MeterCapture.For`) take an optional meter scope (v0.2.0+) so a meter created by an `IMeterFactory` (the ASP.NET Core DI metrics path) can be captured.
- `MeasurementSet`: an immutable, queryable set of measurements with counter and histogram aggregates, sample comparisons, tag and instrument narrowing, and a deterministic snapshot projection.
- `MeterCapture` (meter-wide capture across instruments) and `MeterInspector` (instrument discovery).
- `CapturedMeasurement` (instrument name, value projected to `double`, tags, timestamp).

## Install

```bash
# Most consumers install a test-framework adapter, which pulls this in transitively:
dotnet add package MetricsAssertions.TUnit
```

The full reference is in the [GitHub README](https://github.com/JohnVerheij/MetricsAssertions.TUnit#readme).

## License

MIT.
