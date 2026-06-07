# MetricsAssertions.TUnit

[![CI](https://github.com/JohnVerheij/MetricsAssertions.TUnit/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnVerheij/MetricsAssertions.TUnit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JohnVerheij/MetricsAssertions.TUnit/actions/workflows/codeql.yml/badge.svg)](https://github.com/JohnVerheij/MetricsAssertions.TUnit/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/JohnVerheij/MetricsAssertions.TUnit/badge)](https://scorecard.dev/viewer/?uri=github.com/JohnVerheij/MetricsAssertions.TUnit)
[![codecov](https://codecov.io/gh/JohnVerheij/MetricsAssertions.TUnit/branch/main/graph/badge.svg)](https://codecov.io/gh/JohnVerheij/MetricsAssertions.TUnit)
[![NuGet](https://img.shields.io/nuget/v/MetricsAssertions.TUnit.svg)](https://www.nuget.org/packages/MetricsAssertions.TUnit/)
[![Downloads](https://img.shields.io/nuget/dt/MetricsAssertions.TUnit.svg)](https://www.nuget.org/packages/MetricsAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

TUnit-native assertions over `System.Diagnostics.Metrics` instruments for .NET tests. Fluent entry points over TUnit's `Assert.That(...)` pipeline for asserting on captured measurements, with a framework-agnostic core (`MetricsAssertions`) that a future xUnit, NUnit, or MSTest adapter can reuse. AOT-compatible, trimmable, no runtime reflection in the assertion path. Capture is the first-party `MetricCollector` testing primitive over a `System.Diagnostics.Metrics` instrument: no OpenTelemetry SDK or exporter pipeline.

> **Scope:** Test projects only. Not intended for production code.

---

## Table of contents

- [Why this package](#why-this-package)
- [Install](#install)
- [Package layout](#package-layout)
- [Namespaces](#namespaces)
- [Quick start](#quick-start)
- [Entry points](#entry-points)
- [Failure diagnostics](#failure-diagnostics)
- [Design notes](#design-notes)
- [Stability intent (pre-1.0)](#stability-intent-pre-10)
- [Roadmap](#roadmap)
- [Family compatibility](#family-compatibility)
- [Pair with](#pair-with)
- [Contributing](#contributing)
- [License](#license)

## Why this package

`System.Diagnostics.Metrics` (`Counter<T>`, `Histogram<T>`, `Gauge<T>`, and friends) is how modern .NET code emits metrics. A test wants to assert "the pipeline recorded two `orders.placed` measurements", "the counter total is 5", "a measurement carried `route = /orders`". The BCL ships the capture machinery (`MetricCollector<T>` in `Microsoft.Extensions.Diagnostics.Testing`), but asserting on it is manual: construct a collector, pull a measurement snapshot, then hand-roll the count-and-compare against `Assert`. Every project that does this re-invents the same capture-and-query helper.

`MetricsAssertions.TUnit` absorbs that boilerplate into a reusable capture type and a fluent assertion surface:

- A disposable, per-test `InstrumentCapture` that wraps `MetricCollector<T>` over an instrument and projects its measurements to a uniform `CapturedMeasurement` shape.
- TUnit `Assert.That(capture).HasMeasurementCount(...)` assertions, source-generated via `[GenerateAssertion]`.

The framework-agnostic `MetricsAssertions` core ships separately so non-TUnit consumers can reuse the capture.

**Foundation release (v0.0.1):** ships the capture primitive and a first assertion; the full surface (meter-wide capture, observable and by-name capture, counter totals, tag and delta queries, and more assertions) lands in 0.1.0. See [Roadmap](#roadmap).

## Install

```bash
# TUnit consumers install the adapter; the core is pulled transitively:
dotnet add package MetricsAssertions.TUnit

# Framework-agnostic consumers (rare in test projects) can pull the core directly:
dotnet add package MetricsAssertions
```

**Requirements:** TUnit 1.50.0 or later, .NET 10. AOT-compatible, trimmable.

## Package layout

| Package | Purpose | Depends on |
|---|---|---|
| [`MetricsAssertions`](https://www.nuget.org/packages/MetricsAssertions/) | Framework-agnostic core: the `InstrumentCapture` collector-backed capture type and the `CapturedMeasurement` record | `Microsoft.Extensions.Diagnostics.Testing` |
| [`MetricsAssertions.TUnit`](https://www.nuget.org/packages/MetricsAssertions.TUnit/) | TUnit adapter: fluent `Assert.That(capture)` assertions (`HasMeasurementCount`) | `MetricsAssertions` + `TUnit.Assertions` + `TUnit.Core` |

Install `MetricsAssertions.TUnit` for TUnit test projects; `MetricsAssertions` comes transitively. Adapters for other test frameworks (NUnit, xUnit, MSTest) are not shipped; they would reuse the `MetricsAssertions` core. Open a feature request if you need one.

## Namespaces

| Type / member | Namespace | Auto-imported? |
|---|---|---|
| Fluent entry points (`HasMeasurementCount`) | `TUnit.Assertions.Extensions` | Yes (TUnit auto-imports) |
| Core types (`InstrumentCapture`, `CapturedMeasurement`) | `MetricsAssertions` | No - needs `using MetricsAssertions;` |

A `GlobalUsings.cs` in your test project:

```csharp
global using MetricsAssertions;
```

makes the core namespace available everywhere. The fluent entry points are auto-imported via `TUnit.Assertions.Extensions`, so they need no `using` of their own.

## Quick start

```csharp
using System.Diagnostics.Metrics;
using MetricsAssertions;

using var meter = new Meter("MyCompany.Orders");
var placed = meter.CreateCounter<long>("orders.placed");

// Capture the instrument's measurements for the duration of a test.
using var capture = InstrumentCapture.Of(placed);

placed.Add(1);
placed.Add(1);

await Assert.That(capture).HasMeasurementCount(2);
```

`InstrumentCapture.Of(instrument)` wraps a `MetricCollector<T>` over the instrument and collects every measurement it records. Create one per test with `using` for isolation; disposing it releases the collector.

## Entry points

Capture-level assertion on `Assert.That(capture)` where `capture` is an `InstrumentCapture`:

| Assertion | Description |
|---|---|
| `HasMeasurementCount(n)` | The capture recorded exactly `n` measurements (names the expected and observed counts on failure). |

The `MetricsAssertions` core exposes the capture surface for reading measurements without an assertion:

| Member | Description |
|---|---|
| `InstrumentCapture.Of<T>(instrument, timeProvider?)` | Captures a referenceable `Instrument<T>` via `MetricCollector<T>`. |
| `InstrumentCapture.Measurements` | A snapshot of the captured measurements, each a `CapturedMeasurement`. |
| `InstrumentCapture.Count` | How many measurements were captured. |
| `CapturedMeasurement` | A captured measurement: instrument name, value projected to `double`, tags, and timestamp. |

## Failure diagnostics

`HasMeasurementCount` names both the expected and observed count on failure:

```
Expected the capture to have captured 2 measurement(s)
  but it had 1
```

Every assertion also accepts `.Because(reason)` to attach a domain explanation to the failure, the same as any other TUnit assertion (it is inherited from the base assertion type):

```csharp
await Assert.That(capture).HasMeasurementCount(2).Because("each retry records one measurement");
```

## Design notes

### Why `MetricCollector<T>`, not a raw `MeterListener`

Capture is built on the first-party `MetricCollector<T>` from `Microsoft.Extensions.Diagnostics.Testing`, not a hand-rolled `MeterListener`. `MetricCollector<T>` is the BCL's purpose-built measurement-capture primitive: strongly typed, with built-in measurement snapshots and an injectable `TimeProvider`. Building on it keeps the capture small and correct, at the cost of one NuGet runtime dependency (`Microsoft.Extensions.Diagnostics.Testing`, a Microsoft-shipped package). No OpenTelemetry SDK or exporter pipeline is involved.

### Why per-test capture, disposed via `using`

`InstrumentCapture` is a `using`-scoped handle that attaches its collector on creation and releases it on `Dispose`, with a fresh measurement stream per instance. This keeps capture isolated per test (no process-wide collector leaking measurements across parallel or sequential tests) and bounds the collector's lifetime to the test that needs it.

## Stability intent (pre-1.0)

This is a 0.x release and the public API may evolve.

- **Additive changes** (new entry points, new chain methods) ship in any patch without breaking ApiCompat.
- **Breaking changes** to existing signatures bump the minor version (0.X.0) and are called out in the [CHANGELOG](CHANGELOG.md).
- From 0.1.0, `PackageValidationBaselineVersion` pins to the previous shipped version so ApiCompat catches binary breaks at pack time; `CompatibilitySuppressions.xml` records accepted differences.

The 1.0 milestone signals API stability.

## Roadmap

Shipped in **0.0.1** (foundation): `InstrumentCapture.Of<T>` capture, the `Measurements` snapshot and `Count`, the `CapturedMeasurement` record, and the `HasMeasurementCount` assertion.

Planned for **0.1.0**: `MeterCapture` (meter-wide capture across instruments), `OfObservable` / `OfName` capture, counter and up-down-counter totals, tag-value and captured-after-baseline delta queries, async `WaitForAsync`, and the broader fluent assertions.

Demand-driven; no fixed timeline.

## Family compatibility

The nine assertion-family packages: `LogAssertions.TUnit`, `TimeAssertions.TUnit`, `SnapshotAssertions.TUnit`, `MathAssertions.TUnit`, `JsonAssertions.TUnit`, `SseAssertions.TUnit`, `GrpcAssertions.TUnit`, `TracingAssertions.TUnit`, and `MetricsAssertions.TUnit`: release independently and target the same .NET TFM at any moment (LTS-anchored, multi-target during STS support windows; see the [TFM policy in CONVENTIONS.md](CONVENTIONS.md#tfm-policy) for the rotation schedule). **Mix versions freely.** Each package ships under SemVer with `EnablePackageValidation` strict-mode ApiCompat against its previous baseline, so binary breaks within a version line are caught at pack time.

## Pair with

- **[`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit/)**: fluent assertions over `System.Diagnostics.Activity` spans, the other half of OpenTelemetry signals.
- **[`LogAssertions.TUnit`](https://www.nuget.org/packages/LogAssertions.TUnit/)**: fluent log assertions over `Microsoft.Extensions.Logging.Testing.FakeLogCollector`. Completes the logs / metrics / traces trio.
- **[`TimeAssertions.TUnit`](https://www.nuget.org/packages/TimeAssertions.TUnit/)**: `TimeProvider`-aware time assertions and cross-cutting `.WithinTimeBudget(...)` chain methods.
- **[`SnapshotAssertions.TUnit`](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)**: text-snapshot assertions for API-surface tests and similar deterministic-string scenarios.
- **[`MathAssertions.TUnit`](https://www.nuget.org/packages/MathAssertions.TUnit/)**: tolerance-aware fluent assertions over numeric and geometric types.
- **[`JsonAssertions.TUnit`](https://www.nuget.org/packages/JsonAssertions.TUnit/)**: fluent JSON assertions over `System.Text.Json` and HTTP response bodies.
- **[`SseAssertions.TUnit`](https://www.nuget.org/packages/SseAssertions.TUnit/)**: fluent Server-Sent Events wire-format assertions.
- **[`GrpcAssertions.TUnit`](https://www.nuget.org/packages/GrpcAssertions.TUnit/)**: fluent gRPC outcome assertions plus the `GrpcCallBuilder` test-double helper.

## Contributing

Issues and pull requests welcome. Before opening a PR:

- Run `dotnet build` and `dotnet test` locally; the CI pipeline enforces the same quality bar (zero warnings as errors, 90% line / 90% branch coverage minimum).
- Match the existing code style (`.editorconfig` is authoritative; `dotnet format` covers formatting).
- For new assertions, include a test for both the happy path and a representative failure case.

For larger ideas, open a [Discussion](https://github.com/JohnVerheij/MetricsAssertions.TUnit/discussions) first to align on direction before investing implementation time.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full PR review checklist, and [CONVENTIONS.md](CONVENTIONS.md) for the family-wide code conventions shared across the assertion family.

## License

[MIT](LICENSE). Copyright (c) 2026 John Verheij.
