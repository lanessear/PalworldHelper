# Architecture

The executable hosts an ASP.NET Core server bound to localhost and opens the default browser. SQLite persists non-secret configuration and imported collection data. SSH.NET downloads a temporary read-only copy of `Level.sav`. `SaveImportService` converts/parses it and replaces the selected profile's collection transactionally. `BreedingEngine` performs bounded reverse breadth-first searches from a target child toward owned passive-skill carriers.

The save decoder and breeding dataset are deliberately adapters. Palworld updates can therefore be handled without rewriting the rest of the application.
