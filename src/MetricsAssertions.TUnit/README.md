# MetricsAssertions.TUnit

[![NuGet](https://img.shields.io/nuget/v/MetricsAssertions.TUnit.svg)](https://www.nuget.org/packages/MetricsAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%2011.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet)

> **Scope:** Test projects only. Not intended for production code.

> Part of the **[DotNetAssertions](https://dotnetassertions.dev)** family of assertion extensions for TUnit.

TUnit-native fluent assertions over `System.Diagnostics.Metrics` instruments, built on the first-party `MetricCollector` testing primitive. Targets .NET 10 and .NET 11. AOT-compatible, trimmable, no runtime reflection in the assertion path.

## What ships

Fluent `Assert.That(...).Has*` assertions over three receivers:

| Receiver | Assertions |
|---|---|
| `InstrumentCapture` | counter / up-down-counter totals, measurement count, emptiness, last value (exact or within tolerance), tag presence |
| `MeasurementSet` | totals, counts, histogram sum / average / range, exact, tolerant, and order-insensitive samples, tag-consistency |
| `MeterCapture` | per-instrument totals, counts, and tag presence by name |

Every assertion, by receiver:

- **`InstrumentCapture`** (`Assert.That(capture)`): `HasCounterTotal(n)`, `HasCounterTotalAtLeast(n)`, `HasUpDownCounterValue(n)`, `HasMeasurementCount(n)`, `HasNoMeasurements()`, `HasLastValue(v)`, `HasLastValue(v, tolerance)`, `HasTaggedMeasurement(key, value)`.
- **`MeasurementSet`** (`Assert.That(capture.Measurements)`, `capture.Tagged(...)`, `capture.Since(...)`): `HasCounterTotal(n)`, `HasCounterTotalAtLeast(n)`, `HasMeasurementCount(n)`, `HasNoMeasurements()`, `HasSampleSum(v, tolerance)`, `HasSampleAverage(v, tolerance)`, `HasAllSamplesInRange(min, max)`, `HasSamples(...)`, `HasSamples(expected, tolerance)`, `HasSamplesInAnyOrder(...)`, `HasSamplesInAnyOrder(expected, tolerance)`, `HasEveryMeasurementTagged(key)`, `HasTaggedMeasurement(key, value)`.
- **`MeterCapture`** (`Assert.That(meter)`): `HasCounterTotal(name, n)`, `HasUpDownCounterValue(name, n)`, `HasMeasurementCount(name, n)`, `HasTaggedMeasurement(name, key, value)`.

The framework-agnostic core (`MetricsAssertions`) ships the `InstrumentCapture` / `MeterCapture` capture types, the queryable `MeasurementSet`, `MeterInspector`, and the `CapturedMeasurement` record (instrument name, value projected to `double`, tags, timestamp). The by-name captures (`InstrumentCapture.OfName`, `MeterCapture.For`) take an optional meter scope (v0.2.0+) so a meter created by an `IMeterFactory` (the ASP.NET Core DI metrics path) can be captured. Since v0.4.0, `MeterCapture.For` also takes the clock once for every instrument, and `MeterCapture.For(meter)` / `InstrumentCapture.OfName(meter, name)` capture one `Meter` instance only, so a same-named meter of a test running in parallel is not captured. `MeterCapture.For(name).InCurrentFlow()` records only the measurements emitted in the async flow of the test, which isolates a test from others that emit to the same static meter.

## Install

```bash
dotnet add package MetricsAssertions.TUnit
```

**Requirements:** TUnit 1.85.0 or later, .NET 10 or .NET 11. The framework-agnostic `MetricsAssertions` core comes transitively.

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

## Family

Part of an assertion family for TUnit:

- [GrpcAssertions.TUnit](https://github.com/JohnVerheij/GrpcAssertions.TUnit)
- [JsonAssertions.TUnit](https://github.com/JohnVerheij/JsonAssertions.TUnit)
- [LogAssertions.TUnit](https://github.com/JohnVerheij/LogAssertions.TUnit)
- [MathAssertions.TUnit](https://github.com/JohnVerheij/MathAssertions.TUnit)
- [SnapshotAssertions.TUnit](https://github.com/JohnVerheij/SnapshotAssertions.TUnit)
- [SseAssertions.TUnit](https://github.com/JohnVerheij/SseAssertions.TUnit)
- [TimeAssertions.TUnit](https://github.com/JohnVerheij/TimeAssertions.TUnit)
- [TracingAssertions.TUnit](https://github.com/JohnVerheij/TracingAssertions.TUnit)

## License

MIT.
