# ADR 0002: Local web UI hosted by the desktop executable

- Status: Accepted
- Date: 2026-07-24

## Context

The application needs a modern interface, searchable controls, graph visualization, and a self-contained Windows distribution.

## Decision

Host an ASP.NET Core application on loopback and serve a browser-based UI from the same executable.

## Consequences

The UI can use standard web technologies while installation stays simple. The server must bind locally by default, select a safe port, and manage browser startup gracefully.
