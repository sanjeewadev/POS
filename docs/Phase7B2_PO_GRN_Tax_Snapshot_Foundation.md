# Phase 7B2 — PO and GRN Tax Snapshot Foundation

**Date:** 11 July 2026  
**Status:** Implementation foundation  
**Depends on:** Phase 7B1 — Item Type and Tax Master Foundation

## Purpose

This phase adds safe database storage for immutable purchase-order and GRN tax snapshots.

It does not change PO or GRN screens, calculations, posting, stock, landed cost, supplier ledger behavior, or VAT rules.

## Historical-data rule

All existing PO and GRN rows are marked:

```text
LegacyUnknown
```

New snapshot amounts and classifications remain null. The migration does not parse tax-code text, use `GlobalVatRate`, or apply the current VAT rate to historical transactions.

## Snapshot status values

- `LegacyUnknown` — authoritative snapshot values have not been written.
- `Complete` — the future shared tax engine has written the complete immutable snapshot.

Until the VAT engine and rebuilt PO/GRN workflows are introduced, normal documents remain `LegacyUnknown`.

## PO header foundation

Adds nullable reporting totals:

- taxable amount total
- standard-rated amount
- zero-rated amount
- exempt amount
- out-of-scope amount
- tax snapshot status

## PO line foundation

Adds:

- optional Tax Category relationship
- optional effective Tax Rate relationship
- tax-category code snapshot
- tax-code snapshot
- tax-name snapshot
- tax-rate percentage snapshot
- inclusive/exclusive snapshot
- taxable-amount snapshot
- VAT-amount snapshot
- tax-inclusive-amount snapshot
- tax snapshot status

The new money fields use the `Snapshot` suffix to keep them clearly separate from the existing legacy `TaxAmount`, `VatRatePercent`, and `LineTotal` fields until the calculation migration is completed.

## GRN header foundation

Adds:

- document-level inclusive/exclusive snapshot
- nullable tax-category totals
- freight tax-category code snapshot
- freight taxable amount
- freight VAT amount
- freight snapshot status
- document tax snapshot status

Freight VAT remains inactive until its approved accounting treatment is implemented.

## GRN line foundation

Adds the same immutable tax snapshot structure as PO lines while preserving all current cost, batch, stock, landed-cost, and selling-price fields.

## Migration

Migration ID:

```text
20260711080000_AddPurchaseAndGrnTaxSnapshots
```

The migration is additive and SQLite-compatible. It:

- adds nullable columns;
- adds optional foreign keys to `TaxCategories` and `TaxRates`;
- adds status columns defaulting to `LegacyUnknown`;
- creates indexes for tax-master links and snapshot status;
- does not rebuild or delete current tables;
- does not update existing totals.

## Explicit exclusions

Phase 7B2 does not:

- fix PO or GRN VAT calculations;
- stop tax-code digit parsing;
- populate complete snapshots;
- allocate bill discounts;
- calculate freight VAT;
- update UI controls;
- filter Services from purchasing;
- change stock or landed-cost posting;
- remove legacy tax fields.

Those behaviors are implemented in later controlled phases.
