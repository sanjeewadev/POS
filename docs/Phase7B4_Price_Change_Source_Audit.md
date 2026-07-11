# Phase 7B4 — Price Change Source Audit

Date: 11 July 2026

## Purpose

Extend the existing `PriceChangeHistory` record so future selling-price changes from a posted GRN can be traced to the exact source document and line.

## Added fields

- `SourceDocumentType` — for example `GRN`
- `SourceDocumentId` — source document header ID
- `SourceDocumentLineId` — source document line ID
- `SourceDocumentNo` — human-readable document number

## Safety

- The migration is additive only.
- Existing history rows are preserved.
- Existing rows receive blank source type/number and null source IDs.
- No selling price is changed by this phase.
- No GRN, Item Master, Price Management, Cashier, stock or VAT behavior is changed.

## Future use

During the GRN rebuild, selling-price updates will remain optional and explicit. When a posted GRN changes a retail or wholesale price, the saved `PriceChangeHistory` row will contain the old/new prices plus the GRN header, line and document number.
