# Phase 7D1 — Shared Purchasing VAT Calculation Foundation

**Project:** Advanced POS Development  
**Date:** 11 July 2026  
**Status:** Implementation patch

## Purpose

Phase 7D1 creates one authoritative purchasing VAT calculation path shared by Purchase Orders and GRNs.

It does not redesign the PO or GRN pages. Those page rebuilds remain Phase 7D2 and Phase 7D3.

## Authoritative rules implemented

- The item Tax Category is loaded from `ItemParent.TaxCategoryId`.
- Services are rejected from PO and GRN processing.
- Standard VAT uses the effective `TaxRate` for the document date.
- PO rate date: `OrderDate`.
- GRN rate date: supplier `InvoiceDate`.
- Zero Rated, Exempt and Out of Scope remain separate 0% treatments.
- VAT percentages are never extracted from tax-code text.
- All lines in one purchase document must use one supplier-price mode:
  - VAT Inclusive; or
  - VAT Exclusive.
- Line discounts are calculated before VAT.
- Global bill discount is treated as the exact reduction from the VAT-inclusive document payable and allocated proportionally across lines.
- Each line is rounded to two decimal places using `MidpointRounding.AwayFromZero`.
- Header totals are the sum of saved final line snapshots.

## Saved tax snapshots

Every newly saved PO line and newly posted GRN line receives:

- Tax Category ID
- Tax Rate ID, when rate based
- Tax Category code snapshot
- Tax code snapshot
- Tax name snapshot
- Tax rate percentage snapshot
- VAT-inclusive/exclusive snapshot
- taxable value snapshot
- VAT amount snapshot
- VAT-inclusive value snapshot
- `TaxSnapshotStatus = Complete`

The compatible legacy fields are also populated from the same authoritative result.

New PO and GRN headers receive:

- Standard Rated total
- Zero Rated total
- Exempt total
- Out of Scope total
- taxable total
- VAT total
- total discounts
- final payable
- `TaxSnapshotStatus = Complete`

`TaxableAmountTotal` currently represents the Standard VAT taxable value. Zero Rated remains reported separately.

## Historical documents

No migration is included.

Existing historical PO and GRN records remain unchanged. Rows that were already `LegacyUnknown` stay `LegacyUnknown`. The application does not reconstruct old VAT from code text.

## Global discount behavior

Global bill discount is allocated according to each line's VAT-inclusive value after line discount.

The allocation is rounded by line and whole cents are distributed deterministically by proportional remainder so that:

```text
sum of line global-discount allocations = header global discount
sum of final line VAT-inclusive values = document payable before freight
```

VAT and taxable values are recalculated after the allocation.

## GRN landed cost

For new GRNs:

- the final tax-exclusive line snapshot is the stock-cost base;
- the allocated global discount is already included in that base;
- freight is allocated proportionally over the final tax-exclusive line bases;
- claimable Standard VAT is not included in stock value.

Freight VAT treatment is deliberately not invented in this phase. Freight tax snapshot fields remain `LegacyUnknown` until the accountant-approved rule is implemented.

## UI compatibility

The current PO and GRN pages still contain their old preview calculations and line-level controls. The repository is authoritative when saving/posting.

Until the page rebuilds:

- mixed line VAT modes are blocked at save/post;
- the saved result can differ from an old on-screen preview when a global discount is used;
- the future Phase 7D2/7D3 pages will call the shared calculator for matching live previews.

## Automated checks

A small console verification project is added:

```text
POS.Core.CalculationTests
```

It checks:

1. VAT-exclusive Standard VAT.
2. VAT-inclusive Standard VAT.
3. line discount before VAT.
4. mixed tax categories with proportional global discount.
5. exact allocation and line/header reconciliation.
6. fixed 0% treatments.
7. effective-dated rate selection using SQLite in memory.

Run:

```powershell
dotnet run --project .\POS.Core.CalculationTests\POS.Core.CalculationTests.csproj -c Debug
```

Expected result:

```text
All 7 purchasing tax calculation checks passed.
```

## Not included

- PO page redesign
- GRN page redesign
- freight VAT decision
- non-claimable input VAT
- supplier-service expenses
- sales VAT calculation
- return VAT calculation
- receipt or report changes
- database migration
