# Phase 7B1 — Item Type and Tax Master Foundation

**Date:** 11 July 2026  
**Scope:** Database/model foundation only

## Changes

- Adds authoritative item types: `StockItem` and `Service`.
- Migrates every existing item to `StockItem`.
- Adds the stable `TaxCategory` master.
- Seeds `STANDARD`, `ZERO`, `EXEMPT`, and `OUT_OF_SCOPE`.
- Extends `TaxRate` with category, effective-date, and audit fields.
- Adds an optional item-to-tax-category relationship.
- Adds store VAT-registration, TIN, VAT number, and tax-invoice prefix fields.
- Maps only unambiguous legacy `VAT-STD` rows/items to `STANDARD`.
- Leaves `TAX-FREE`, reduced-rate, and unknown legacy codes unclassified for later review.

## Not changed

- Item Master UI
- Tax Rate UI
- PO or GRN calculations
- Cashier calculations
- Stock posting
- Sales or return VAT snapshots
- Receipt or report output
- `GlobalVatRate` compatibility field

## SQLite safety

The migration uses additive SQLite SQL. Nullable foreign-key columns are added with inline `REFERENCES` clauses, avoiding table rebuilds.

Validation against a copy of the current SQLite schema confirmed:

- schema creation succeeded;
- four categories were seeded exactly once;
- existing items defaulted to `StockItem`;
- `VAT-STD` mapped to `STANDARD`;
- ambiguous codes remained null;
- foreign-key check returned no violations;
- integrity check returned `ok`;
- the Down path restored the original schema in the validation copy.

A local .NET build was not available in the preparation environment. Debug and Release builds must be run on the development machine before commit.
