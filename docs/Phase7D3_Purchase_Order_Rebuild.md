# Phase 7D3 — Purchase Order Rebuild

Date: 11 July 2026

## Scope

Phase 7D3 rebuilds the Purchase Order page on top of the approved Stock Item, Service, Tax Master, tax-snapshot and shared purchasing-VAT foundations.

No database migration is required.

## Purchase Order rules

- Purchase Orders accept Stock Items only.
- Services remain excluded from inventory purchasing.
- A PO does not update stock, cost prices, selling prices or the supplier ledger.
- A direct GRN remains available without a PO.
- Supplier-approved item/variant relationships remain required.
- The current small-store workflow saves a new PO directly as `Approved`.
- Approved POs may be loaded into GRN.
- The current GRN workflow closes a linked PO when receiving is posted.
- Cancelled or received/closed POs cannot be unsafely edited.

A separate draft-edit workflow is deliberately not introduced in this phase because the current PO page has no safe saved-document edit route. This avoids creating draft records that the user cannot reopen correctly.

## Authoritative VAT behavior

The page uses the same `PurchasingTaxService` used by GRN.

- One document-level option controls whether supplier prices include VAT.
- Per-line VAT percentages and VAT-inclusive checkboxes are no longer editable.
- The applicable tax profile is resolved from Item Master and effective-dated Tax Rate Management using the PO date.
- Changing the PO date refreshes the tax profile for loaded variants and lines.
- Line discounts are applied before VAT.
- The document discount is allocated proportionally across lines.
- Standard VAT, Zero Rated, Exempt and Out of Scope remain separate.
- The repository recalculates and writes the final immutable tax snapshots when saving.

## Page layout

The compact classic layout contains:

1. Document Setup
2. Stock Item / Variant Entry
3. Purchase Order Lines
4. Document Adjustment, VAT Summary and Estimated Totals

The line grid displays:

- quantity;
- expected supplier cost;
- line discount;
- allocated bill discount;
- tax category;
- taxable value;
- VAT rate and amount;
- final line total.

## Compatibility

- Existing PO and GRN database fields remain unchanged.
- Existing PO dashboard and cancellation behavior remain available.
- Existing historical POs remain readable.
- The authoritative save-time repository calculation remains the final source of truth.
- The existing purchasing calculation test suite continues to validate inclusive/exclusive VAT, discounts, category totals and effective-dated rates.
