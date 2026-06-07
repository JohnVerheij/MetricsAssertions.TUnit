# MetricsAssertions.TUnit

[![CI](https://github.com/JohnVerheij/MetricsAssertions.TUnit/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnVerheij/MetricsAssertions.TUnit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JohnVerheij/MetricsAssertions.TUnit/actions/workflows/codeql.yml/badge.svg)](https://github.com/JohnVerheij/MetricsAssertions.TUnit/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/JohnVerheij/MetricsAssertions.TUnit/badge)](https://scorecard.dev/viewer/?uri=github.com/JohnVerheij/MetricsAssertions.TUnit)
[![codecov](https://codecov.io/gh/JohnVerheij/MetricsAssertions.TUnit/branch/main/graph/badge.svg)](https://codecov.io/gh/JohnVerheij/MetricsAssertions.TUnit)
[![NuGet](https://img.shields.io/nuget/v/MetricsAssertions.TUnit.svg)](https://www.nuget.org/packages/MetricsAssertions.TUnit/)
[![Downloads](https://img.shields.io/nuget/dt/MetricsAssertions.TUnit.svg)](https://www.nuget.org/packages/MetricsAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

TUnit-native fluent assertions over `System.Diagnostics.Metrics` instruments, built on the first-party `MetricCollector` testing primitive.

> **Scope:** Test projects only. Not intended for production code.

---

## Why this package

`System.Diagnostics.Metrics` (`Counter<T>`, `Histogram<T>`, `Gauge<T>`, and friends) is how modern .NET code emits metrics, but verifying what an instrument recorded in a test means hand-rolling a `MetricCollector<T>` and digging through measurement snapshots. This package captures the measurements an instrument records into a disposable, per-test `InstrumentCapture` and exposes them through fluent `Assert.That(capture)` assertions, so a test reads as the metric it is checking.

Capture is the BCL testing primitive (`MetricCollector<T>` from `Microsoft.Extensions.Diagnostics.Testing`); there is no OpenTelemetry SDK or exporter pipeline involved. The assertion path is AOT-clean (no runtime reflection).

## Foundation release (v0.0.1)

This is the foundation release. It ships the core capture primitive and a first assertion so the package and its surface are real and pinned:

- **`MetricsAssertions` (core):** `InstrumentCapture.Of<T>(Instrument<T>)` captures a referenceable instrument; `Measurements` is the projected snapshot and `Count` the measurement count. Each measurement is a `CapturedMeasurement` (instrument name, value projected to `double`, tags, timestamp).
- **`MetricsAssertions.TUnit` (adapter):** `Assert.That(capture).HasMeasurementCount(n)`.

The full surface (meter-wide capture, observable and by-name capture, counter and up-down-counter totals, tag-value and delta queries, async waiting, and the broader assertion set) lands in **0.1.0**.

## Install

```bash
# TUnit consumers install the adapter; the core comes transitively:
dotnet add package MetricsAssertions.TUnit

# Framework-agnostic consumers can pull the core directly:
dotnet add package MetricsAssertions
```

**Requirements:** TUnit 1.48.6 or later, .NET 10. AOT-compatible, trimmable.

## Quick start

```csharp
using System.Diagnostics.Metrics;
using MetricsAssertions;

using var meter = new Meter("MyCompany.Orders");
var placed = meter.CreateCounter<long>("orders.placed");
using var capture = InstrumentCapture.Of(placed);

placed.Add(1);
placed.Add(1);

await Assert.That(capture).HasMeasurementCount(2);
```

## Package layout

| Package | Contents | Dependency |
|---|---|---|
| `MetricsAssertions` | Framework-agnostic capture: `InstrumentCapture`, `CapturedMeasurement` | `Microsoft.Extensions.Diagnostics.Testing` |
| `MetricsAssertions.TUnit` | The TUnit assertion entry points (`[GenerateAssertion]`) | the core, `TUnit.Assertions` |

## Roadmap

`0.1.0` completes the surface: `MeterCapture` (meter-wide), `OfObservable` / `OfName` capture, counter and up-down-counter totals, tag-value and captured-after-baseline delta queries, async `WaitForAsync`, and the broader fluent assertions.

## Family

One of a family of TUnit-native assertion packages (Time, Json, Snapshot, Grpc, Sse, Log, Tracing, Metrics), all sharing the same conventions and `[GenerateAssertion]` approach.

## Contributing

Issues and pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and [CONVENTIONS.md](CONVENTIONS.md).

## License

MIT. See [LICENSE](LICENSE).
