# MetricsAssertions.TUnit

[![NuGet](https://img.shields.io/nuget/v/MetricsAssertions.TUnit.svg)](https://www.nuget.org/packages/MetricsAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

> Part of the **[DotNetAssertions](https://dotnetassertions.dev)** family of assertion extensions for TUnit.

TUnit-native fluent assertions over `System.Diagnostics.Metrics` instruments, built on the first-party `MetricCollector` testing primitive. AOT-compatible, trimmable, no runtime reflection in the assertion path.

## What ships

Fluent `Assert.That(...).Has*` assertions over three receivers:

| Receiver | Assertions |
|---|---|
| `InstrumentCapture` | counter / up-down-counter totals, measurement count, emptiness, last value (exact or within tolerance), tag presence |
| `MeasurementSet` | totals, counts, histogram sum / average / range, exact, tolerant, and order-insensitive samples, tag-consistency |
| `MeterCapture` | per-instrument totals, counts, and tag presence by name |

The framework-agnostic core (`MetricsAssertions`) ships the `InstrumentCapture` / `MeterCapture` capture types, the queryable `MeasurementSet`, `MeterInspector`, and the `CapturedMeasurement` record (instrument name, value projected to `double`, tags, timestamp). The by-name captures (`InstrumentCapture.OfName`, `MeterCapture.For`) take an optional meter scope (v0.2.0+) so a meter created by an `IMeterFactory` (the ASP.NET Core DI metrics path) can be captured.

## Install

```bash
dotnet add package MetricsAssertions.TUnit
```

**Requirements:** TUnit 1.62.0 or later, .NET 10. The framework-agnostic `MetricsAssertions` core comes transitively.

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

The full reference is in the [GitHub README](https://github.com/JohnVerheij/MetricsAssertions.TUnit#readme).

## License

MIT.
