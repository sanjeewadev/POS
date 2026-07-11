# Phase 7B3 — Sales and Return Tax Snapshot Foundation

**Date:** 11 July 2026  
**Status:** Implementation foundation  
**Depends on:** Phase 7B1 and Phase 7B2

## Purpose

This phase adds safe database storage for immutable sales, customer-return and supplier-return tax snapshots.

It does not change Cashier calculations, service sales, stock deduction, receipt printing, tax-invoice printing, return posting, supplier-return posting or reports.

## Historical-data rule

Every existing sales and return row is marked:

```text
LegacyUnknown
```

New tax amounts and classifications remain null. The migration does not reconstruct VAT from current rates, item tax codes, `GlobalVatRate`, invoice totals or tax-code text.

## Sales header foundation

Adds:

- receipt versus tax-invoice document type;
- optional formal tax-invoice number;
- VAT-registration snapshot;
- supplier TIN/VAT snapshots;
- customer TIN/VAT/address snapshots;
- taxable, VAT, standard-rated, zero-rated, exempt and out-of-scope totals;
- tax snapshot status.

Historical sales default to `Receipt` only as a document-type compatibility value. Their tax status remains `LegacyUnknown`.

## Sales line foundation

Adds:

- item-type snapshot;
- optional Tax Category and effective Tax Rate links;
- tax-category/code/name snapshots;
- rate and inclusive/exclusive snapshots;
- taxable, VAT and tax-inclusive amount snapshots;
- tax snapshot status.

`ItemBatchId` remains nullable and its navigation is made optional. Later Cashier work will require batches for Stock Items and allow null batches for Services.

## Customer return foundation

Header additions:

- optional link to the original sales header;
- Return versus Credit Note document type;
- optional credit-note number;
- tax-category totals;
- tax snapshot status.

Line additions:

- optional link to the original sales line;
- optional item-batch link;
- item-type snapshot;
- immutable tax snapshots;
- full original sales-line tax values for exact or partial reversal;
- tax snapshot status.

A future Service refund will use a null batch and create no inventory movement.

## Supplier return foundation

Adds header tax totals and line-level tax snapshots copied from the source GRN. Original source tax values are retained separately so partial supplier returns can be reconciled correctly.

Supplier returns remain inventory-based and therefore continue to require exact Stock Item batches.

## Migration

Migration ID:

```text
20260711090000_AddSalesAndReturnTaxSnapshots
```

The migration is additive and SQLite-compatible. It:

- adds nullable snapshot columns;
- adds optional foreign keys;
- adds `LegacyUnknown` status defaults;
- creates lookup indexes;
- preserves all current rows and IDs;
- does not rebuild or remove current tables;
- does not alter stock or financial totals.

## Explicit exclusions

Phase 7B3 does not:

- calculate VAT on new sales;
- populate complete snapshots;
- sell Services;
- change stock validation or deduction;
- implement real customer returns or credit notes;
- reverse VAT;
- allocate discounts;
- print tax invoices;
- remove legacy fields;
- implement tax reports.

Those behaviors belong to later controlled development phases.
