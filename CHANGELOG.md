# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[unreleased]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/compare/v0.0.1...HEAD
[0.0.1]: https://github.com/JohnVerheij/MetricsAssertions.TUnit/releases/tag/v0.0.1
