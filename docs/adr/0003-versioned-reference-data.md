# ADR 0003: Versioned reference data in SQLite

- Status: Accepted
- Date: 2026-07-24

## Context

Breeding combinations and Pal metadata change between Palworld versions. A fixed JSON file cannot reliably represent history and user corrections.

## Decision

Store normalized reference data in SQLite and associate imports with a data-set version. JSON remains an interchange format rather than the runtime source of truth.

## Consequences

Imports require validation and migrations, but version comparisons, corrections, and efficient graph queries become possible.
