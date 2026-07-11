# Phase 7C1 — Tax Category and Tax Rate Management

Date: 11 July 2026

## Scope

This phase rebuilds the BackOffice Tax Rate page around the approved Sri Lanka-only VAT architecture.

It does not yet change PO, GRN, Cashier, return, receipt, report or Item Master calculations.

## Authoritative categories

Only these four normal categories are shown:

- Standard VAT
- Zero Rated
- Exempt
- Out of Scope

The categories are fixed system treatments. Users cannot create additional tax categories from this page.

Zero Rated, Exempt and Out of Scope do not require editable percentage records. They remain separate because their legal and reporting meanings differ.

## Standard VAT rate versions

Standard VAT is managed using effective-dated versions.

Each version records:

- generated stable rate code;
- percentage;
- effective-from date;
- optional inclusive effective-to date;
- active/inactive status;
- change reason;
- created/updated user and timestamps.

Active periods in the same category cannot overlap.

The initial unambiguous `VAT-STD` 18% record is associated with Standard VAT and, when its start date is missing, is normalized to 1 January 2024.

## History safety

When a rate version is referenced by legacy items or transaction snapshot fields:

- percentage cannot be changed;
- effective-from date cannot be changed;
- the effective-to date may be set to close the historical period;
- a new version must be created for a later rate;
- deletion is blocked.

The currently effective rate cannot be deactivated unless another active rate covers the current date.

Historical transaction snapshots are never recalculated by this page.

## Legacy records

Unclassified records such as old `TAX-FREE` or reduced-rate codes remain visible under **Legacy / Unclassified**.

They can be reviewed, deactivated or deleted only when unused. They are not treated as authoritative categories. Item-level reclassification is deferred to the Item Master rebuild.

## Rate lookup foundation

`TaxRateRepository` now provides effective-date lookup by stable category code. It does not extract percentages from tax-code text.

PO and GRN repositories are intentionally not rewired in this patch. Their current calculation paths will be replaced during the approved PO/GRN and shared VAT-engine phases.

## Files changed

- `POS.Core/Models/TaxRate.cs`
- `POS.Core/Repositories/TaxRateRepository.cs`
- `POS.BackOffice.UI/ViewModels/TaxRateViewModel.cs`
- `POS.BackOffice.UI/Views/Pages/InventoryPages/TaxRateView.xaml`

No database migration is required because Phase 7B1 already added all required columns and tables.

## Verification

1. Build Debug and Release with zero errors.
2. Open BackOffice and Tax Rate.
3. Confirm four fixed categories plus Legacy / Unclassified.
4. Confirm Standard VAT shows the existing 18% version and effective date.
5. Confirm Zero Rated, Exempt and Out of Scope show no editable rate form.
6. Confirm overlapping Standard VAT periods are rejected.
7. Confirm a used rate cannot have its percentage or start date changed.
8. Confirm BackOffice and Cashier continue to open normally.

## Legal baseline reference

Sri Lanka Inland Revenue Department VAT information records the standard rate as 18% from 1 January 2024:

`https://www.ird.gov.lk/en/type%20of%20taxes/sitepages/value%20added%20tax%20%28vat%29.aspx`
