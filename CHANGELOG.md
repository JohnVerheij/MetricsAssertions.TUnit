# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

### Changed

- **Breaking:** `InstrumentCapture.Measurements` now returns a `MeasurementSet` instead of
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

[unreleased]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/compare/v0.0.1...v0.1.0
[0.0.1]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/releases/tag/v0.0.1
