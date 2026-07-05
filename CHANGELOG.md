# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> *History note:* All version sections were reformatted on 2026-07-05 for one-time
> [CONVENTIONS &sect;CHANGELOG](CONVENTIONS.md) conformance: forbidden sub-headers folded into
> the six Keep a Changelog headers, non-user-facing content removed (internal refactors, test
> counts, coverage numbers, CI and build hygiene, governance churn, roadmap notes), bullets
> kept in past-tense active voice with code-formatted API leads. The nuget.org Release Notes
> tab and the GitHub Release for each shipped version are unchanged. A CI `changelog-lint` gate
> keeps future sections conforming; each is frozen per Rule 7 once shipped.

## [Unreleased]

## [0.3.0] - 2026-06-15: tolerance overloads on the value assertions

Minor release. Adds absolute-tolerance overloads to the value assertions, closing the asymmetry where the histogram aggregates (`HasSampleSum` / `HasSampleAverage`) accepted a tolerance but the value assertions compared doubles exactly. Purely additive; the `0.2.0` ApiCompat baseline is preserved.

### Added

- **`HasLastValue(double expected, double tolerance)`** on an `InstrumentCapture` asserts the most recent value within an absolute tolerance, the tolerant counterpart of `HasLastValue(double)` for gauges fed computed doubles (where `0.1 + 0.2 != 0.3` exact comparison flakes).
- **`HasSamples(IReadOnlyList<double> expected, double tolerance)`** and **`HasSamplesInAnyOrder(IReadOnlyList<double> expected, double tolerance)`** on a `MeasurementSet` assert the samples within an absolute per-sample tolerance, in order and order-independent respectively. The framework-agnostic core gains the matching `MeasurementSet.SamplesEqual(expected, tolerance)` / `SamplesEquivalentTo(expected, tolerance)` comparison primitives. Every tolerance overload rejects a non-finite or negative tolerance with `ArgumentOutOfRangeException`, so an invalid tolerance fails fast instead of silently matching everything (`NaN`) or nothing (negative).

### Changed

- **`MeasurementSet.Total`** now documents that the sum is rounded to a `long` with banker's rounding (round half to even), so a `Counter<double>` with a fractional total is evaluated as an integer. For an exact fractional total, compare `Sum` directly or use `HasSampleSum(expected, tolerance)`.

## [0.2.0] - 2026-06-12: capture meters created by an IMeterFactory

Minor release. Adds scope-aware capture so a meter created by an `IMeterFactory` (the standard ASP.NET Core DI metrics path) can be captured by name. Purely additive.

### Added

- **`InstrumentCapture.OfName<T>(object? meterScope, string meterName, string instrumentName, TimeProvider?)`** and **`MeterCapture.For(string meterName, object? meterScope)`** capture an instrument on a meter whose `Meter.Scope` equals `meterScope`. A meter created by an `IMeterFactory` carries the factory as its scope, so pass that factory to capture it; the existing no-scope overloads match only a meter created directly (`new Meter(name)`) and silently capture nothing from a factory-created one. The no-scope overloads now delegate to the scope-aware ones with a null scope, so existing behavior is unchanged. The by-reference `MeterCapture.Add(Instrument<T>)` now also requires the instrument's meter scope to match the bundle's scope (it already required the meter name to match), so an instrument from a same-named but differently-scoped meter is rejected.

## [0.1.0] - 2026-06-07: full assertion surface

The full surface: a queryable `MeasurementSet`, multi-instrument `MeterCapture`, meter-introspection
`MeterInspector`, baseline deltas, and the complete `Assert.That` assertion vocabulary across all three.

### Added

- **`MeasurementSet` (core):** an immutable, queryable set of captured measurements with counter-style
  (`Total`) and histogram-style (`Sum`/`Min`/`Max`/`Average`) aggregates, raw `Values`/`All`, ordered and
  order-insensitive sample comparisons, tag and instrument narrowing (`Tagged`/`ForInstrument`), range and
  tag predicates, and a deterministic `ToSnapshotString` projection.
- **`MeterCapture` (core):** a meter-wide bundle composed from per-instrument captures, built fluently
  with `For` + `Add`, queried per instrument or across all via `Measurements`, with `RecordObservable`
  for observable gauges.
- **`MeterInspector` (core):** discovers which instruments a meter publishes
  (`PublishedInstrumentNames`/`IsPublished`/`PublishesAll`) via a short-lived `MeterListener`.
- **`InstrumentCapture` (core):** expanded with `OfName`/`OfObservable` construction, `Total`/`LastValue`,
  tag queries (`Tagged`/`HasMeasurementTagged`), baseline deltas (`Snapshot`/`Since` + `MeasurementBaseline`),
  `RecordObservable`, and `WaitForAsync`.
- **`MetricsAssertions.TUnit` (adapter):** the full `Assert.That(...).Has*` vocabulary (counter and
  up-down-counter totals, measurement counts, emptiness, last value, histogram sum/average/range, exact and
  order-insensitive sample sets, and tag-consistency) over `InstrumentCapture`, `MeasurementSet`, and
  `MeterCapture`.
- **Failure diagnostics:** every assertion dumps the captured measurements (instrument, value, tags,
  timestamp) under the failure, so a mismatch shows what was actually recorded, not only the mismatched
  scalar.
- **Hardening:** tolerance arguments are validated (finite, non-negative), inverted range bounds are
  rejected, baselines are bound to their originating capture, duplicate `MeterCapture` instrument names
  dispose the prior capture, and unknown meter-capture instruments fail as assertions rather than throwing.

### Changed

- **BREAKING:** `InstrumentCapture.Measurements` now returns a `MeasurementSet` instead of
  `IReadOnlyList<CapturedMeasurement>`. Use `Measurements.All` for the underlying list, or the new query
  surface directly.

## [0.0.1] - 2026-06-07: foundation release

Foundation release. Reserves the package names and ships a real, minimal surface: single-instrument
capture and a first assertion. The full surface lands in 0.1.0.

### Added

- **`MetricsAssertions` (core):** `InstrumentCapture` captures the measurements a referenceable
  `Instrument<T>` records, built on the first-party `MetricCollector<T>` testing primitive.
  `Of<T>(Instrument<T>, TimeProvider?)` constructs a capture; `Measurements` is the projected
  snapshot and `Count` the measurement count. Each measurement is a `CapturedMeasurement`
  (instrument name, value projected to `double`, tags, timestamp).
- **`MetricsAssertions.TUnit` (adapter):** `Assert.That(capture).HasMeasurementCount(n)`, generated
  via TUnit's `[GenerateAssertion]` source generator.

[unreleased]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/compare/v0.0.1...v0.1.0
[0.0.1]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/releases/tag/v0.0.1
