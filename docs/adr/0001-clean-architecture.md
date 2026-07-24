# ADR 0001: Modular clean architecture

- Status: Accepted
- Date: 2026-07-24

## Context

The first prototype combined UI, persistence, import, and planning concerns. The long-term application needs replaceable save converters, reliable tests, and room for plugins.

## Decision

Use separate projects for the executable, domain core, persistence, save import, and plugin contracts. Dependencies point inward toward the domain.

## Consequences

The initial repository contains more projects and interfaces, but feature code remains testable and infrastructure can change without rewriting business logic.
