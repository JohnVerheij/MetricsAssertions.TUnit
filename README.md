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

> Part of the **[DotNetAssertions](https://dotnetassertions.dev)** family of assertion extensions for TUnit.

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

- A disposable, per-test `InstrumentCapture` that wraps `MetricCollector<T>` over an instrument and projects its measurements to a uniform `CapturedMeasurement` shape, exposed as a queryable `MeasurementSet`.
- A full TUnit `Assert.That(...).Has*` vocabulary over captures, measurement sets, and multi-instrument `MeterCapture` bundles, source-generated via `[GenerateAssertion]`.

The framework-agnostic `MetricsAssertions` core ships separately so non-TUnit consumers can reuse the capture and query surface.

## Install

```bash
# TUnit consumers install the adapter; the core is pulled transitively:
dotnet add package MetricsAssertions.TUnit

# Framework-agnostic consumers (rare in test projects) can pull the core directly:
dotnet add package MetricsAssertions
```

**Requirements:** TUnit 1.61.29 or later, .NET 10. AOT-compatible, trimmable.

## Package layout

| Package | Purpose | Depends on |
|---|---|---|
| [`MetricsAssertions`](https://www.nuget.org/packages/MetricsAssertions/) | Framework-agnostic core: the `InstrumentCapture` / `MeterCapture` capture types, the queryable `MeasurementSet`, `MeterInspector`, and the `CapturedMeasurement` record | `Microsoft.Extensions.Diagnostics.Testing` |
| [`MetricsAssertions.TUnit`](https://www.nuget.org/packages/MetricsAssertions.TUnit/) | TUnit adapter: fluent `Assert.That(...)` assertions over captures, measurement sets, and meter bundles | `MetricsAssertions` + `TUnit.Assertions` + `TUnit.Core` |

Install `MetricsAssertions.TUnit` for TUnit test projects; `MetricsAssertions` comes transitively. Adapters for other test frameworks (NUnit, xUnit, MSTest) are not shipped; they would reuse the `MetricsAssertions` core. Open a feature request if you need one.

## Namespaces

| Type / member | Namespace | Auto-imported? |
|---|---|---|
| Fluent entry points (`HasCounterTotal`, `HasMeasurementCount`, ...) | `TUnit.Assertions.Extensions` | Yes (TUnit auto-imports) |
| Core types (`InstrumentCapture`, `MeasurementSet`, `MeterCapture`, `MeterInspector`, `CapturedMeasurement`) | `MetricsAssertions` | No - needs `using MetricsAssertions;` |

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

placed.Add(2);
placed.Add(3);

await Assert.That(capture).HasCounterTotal(5);
await Assert.That(capture).HasMeasurementCount(2);
```

`InstrumentCapture.Of(instrument)` wraps a `MetricCollector<T>` over the instrument and collects every measurement it records. Create one per test with `using` for isolation; disposing it releases the collector.

## Entry points

Assertions on `Assert.That(capture)` where `capture` is an `InstrumentCapture`:

| Assertion | Description |
|---|---|
| `HasCounterTotal(n)` | The net total of the captured values equals `n`. |
| `HasCounterTotalAtLeast(n)` | The net total is at least `n`. |
| `HasUpDownCounterValue(n)` | The net up-down-counter value equals `n`. |
| `HasMeasurementCount(n)` | Exactly `n` measurements were captured. |
| `HasNoMeasurements()` | No measurements were captured. |
| `HasLastValue(v)` / `HasLastValue(v, tolerance)` | The most recently captured value equals `v` (exactly, or within an absolute tolerance for computed doubles). |
| `HasTaggedMeasurement(key, value)` | At least one measurement carries the tag `key = value`. |

Assertions on `Assert.That(set)` where `set` is a `MeasurementSet` (e.g. `capture.Measurements`, `capture.Tagged(...)`, `capture.Since(...)`):

| Assertion | Description |
|---|---|
| `HasCounterTotal(n)` / `HasCounterTotalAtLeast(n)` | The net total equals / is at least `n`. |
| `HasMeasurementCount(n)` / `HasNoMeasurements()` | The set holds exactly `n` measurements / is empty. |
| `HasSampleSum(v, tolerance?)` / `HasSampleAverage(v, tolerance?)` | The histogram sample sum / mean equals `v` within an optional tolerance. |
| `HasAllSamplesInRange(min, max)` | Every sample lies within the inclusive range. |
| `HasSamples(...)` / `HasSamplesInAnyOrder(...)` | The samples equal the expected values, in order / in any order. A `(IReadOnlyList<double> expected, double tolerance)` overload of each compares within an absolute per-sample tolerance. |
| `HasEveryMeasurementTagged(key)` | Every measurement carries a tag with the given key. |
| `HasTaggedMeasurement(key, value)` | At least one measurement carries the tag `key = value`. |

Assertions on `Assert.That(meter)` where `meter` is a multi-instrument `MeterCapture`, addressing one bundled instrument by name:

| Assertion | Description |
|---|---|
| `HasCounterTotal(name, n)` / `HasUpDownCounterValue(name, n)` | The named counter's net total / value equals `n`. |
| `HasMeasurementCount(name, n)` | The named instrument captured exactly `n` measurements. |
| `HasTaggedMeasurement(name, key, value)` | The named instrument has a measurement tagged `key = value`. |

The `MetricsAssertions` core exposes the capture and query surface for reading measurements without an assertion:

| Member | Description |
|---|---|
| `InstrumentCapture.Of<T>` / `OfObservable<T>` / `OfName<T>` | Captures a referenceable, observable, or by-name instrument via `MetricCollector<T>`. |
| `InstrumentCapture.OfName<T>(meterScope, ...)` / `MeterCapture.For(name, meterScope)` *(v0.2.0+)* | Capture a meter created by an `IMeterFactory` (the ASP.NET Core DI metrics path): pass the factory as `meterScope`. The no-scope overloads match only a directly-created meter (`new Meter(name)`) and would capture nothing from a factory-created one. |
| `InstrumentCapture.Measurements` | The captured measurements as a queryable `MeasurementSet`. |
| `InstrumentCapture.Total` / `Count` / `LastValue` / `Tagged` / `HasMeasurementTagged` | Convenience reads over the capture. |
| `InstrumentCapture.Snapshot` / `Since` | Take a `MeasurementBaseline` and query only the measurements recorded after it (an action's delta). |
| `InstrumentCapture.RecordObservable` / `WaitForAsync` | Pull observable-gauge values; await a measurement count. |
| `MeasurementSet` | An immutable, queryable set: `Total`/`Sum`/`Min`/`Max`/`Average`/`Values`, `Tagged`/`ForInstrument`, sample comparisons, and `ToSnapshotString`. |
| `MeterCapture.For` + `Add` | Build a meter-wide bundle and query per instrument or across all via `Measurements`. |
| `MeterInspector` | Discover a meter's published instruments (`PublishedInstrumentNames` / `IsPublished` / `PublishesAll`). |
| `CapturedMeasurement` | A captured measurement: instrument name, value projected to `double`, tags, and timestamp. |

## Failure diagnostics

Every assertion dumps the captured measurements (instrument, value, tags, timestamp) under the failure, so a mismatch shows what was actually recorded rather than only the mismatched scalar:

```
Expected the capture to have captured 2 measurement(s)
  but it had 1
  captured:
    orders.placed = 3 {route=/orders} @ 2026-06-07T10:15:00.0000000+00:00
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

### Values are projected to `double`

Every measurement's value is projected to `double` (via `Convert.ToDouble`) so heterogeneous instrument types share one query and assertion surface. `double` represents integers exactly only up to 2^53 and cannot represent every decimal exactly, so an exact-equality assertion on a very large `long` counter, or on a `decimal` (for example money-as-metric) histogram, can surprise. For counts and durations this is a non-issue; for non-integer values, prefer the tolerance overloads, which take an explicit absolute tolerance: `HasSampleSum` / `HasSampleAverage`, and (since 0.3.0) `HasLastValue(v, tolerance)`, `HasSamples(expected, tolerance)`, and `HasSamplesInAnyOrder(expected, tolerance)`.

Counter totals are evaluated as integers: `MeasurementSet.Total` (and the `HasCounterTotal` assertions over it) round the sum to a `long` with banker's rounding, so a `Counter<double>` with a fractional total is compared as an integer. For an exact fractional total, compare `Sum` directly or use `HasSampleSum(expected, tolerance)`.

### Assertions are kind-named, not kind-checked

The capture is typed by the instrument's value type (`T`), not its kind, and every value is projected to `double`, so the assertion surface is uniform across counters, histograms, and gauges. The kind-named terminators (`HasCounterTotal`, `HasSampleSum`, `HasLastValue`) signal intent but do not enforce it: calling `HasSampleSum` on a counter capture, or `HasCounterTotal` on a histogram, computes a defined but meaningless number rather than refusing to apply. Metric tests are normally written by whoever created the instrument, so this is a deliberate simplicity-over-safety tradeoff. As a partial guard, `HasCounterTotal` rejects negative deltas, since a true counter never decrements. Enforcing kind end to end (a capture that remembers its instrument kind) is a post-1.0 consideration.

## Stability intent (pre-1.0)

This is a 0.x release and the public API may evolve.

- **Additive changes** (new entry points, new chain methods) ship in any patch without breaking ApiCompat.
- **Breaking changes** to existing signatures bump the minor version (0.X.0) and are called out in the [CHANGELOG](CHANGELOG.md).
- From 0.1.0, `PackageValidationBaselineVersion` pins to the previous shipped version so ApiCompat catches binary breaks at pack time; `CompatibilitySuppressions.xml` records accepted differences.

The 1.0 milestone signals API stability.

## Roadmap

Shipped in **0.1.0** (full surface): the queryable `MeasurementSet`, multi-instrument `MeterCapture`, meter-introspection `MeterInspector`, `OfObservable` / `OfName` and baseline-delta capture, `WaitForAsync`, and the full `Assert.That` assertion vocabulary across captures, sets, and meters.

Further assertions and capture conveniences are demand-driven; open a feature request or [Discussion](https://github.com/JohnVerheij/MetricsAssertions.TUnit/discussions) if you hit a gap. No fixed timeline.

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
