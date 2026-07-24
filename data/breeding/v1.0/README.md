# Palworld Breeding Dataset v1.0

**Dataset status:** Current  
**Dataset date:** 24 July 2026  
**Version:** 1.0

This dataset contains all breeding combinations captured in the provided IndexedDB export.

## Dataset statistics

- 44,551 source rows
- 44,551 unique breeding results
- 44,551 successful results
- 0 failed results
- 387 unique Pal names

## Repository format

The repository dataset uses compact schema version 2. Pal names are stored once in the `names` array. Every entry in `results` contains three zero-based indexes in this order:

```text
[parent1, parent2, child]
```

Expected dataset file:

```text
palworld_breeding_results_v1.0_2026-07-24.compact.json
```

The version, date, counts, schema description, and SHA-256 checksums are recorded in `manifest.json`.

## Provenance

The dataset was exported from IndexedDB on 24 July 2026 and supplied by the human project owner. AI converted it into the compact repository format. The conversion changes only representation; all 44,551 breeding combinations remain present.
