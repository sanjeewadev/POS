# Phase 7E1 — Shared Sales VAT Engine Foundation

Date: 2026-07-11

## Purpose

This phase introduces one authoritative sales VAT calculation service before the Cashier UI is changed for Services and tax-invoice printing.

No database migration is included. The required sales and return snapshot columns already exist from Phase 7B3.

## Included

- Effective-dated sales tax profile resolution from `TaxCategory` and `TaxRate`
- Support for both `StockItem` and `Service`
- VAT-inclusive retail and wholesale selling-price calculations
- VAT calculated after line discounts
- Proportional invoice-discount allocation foundation
- Immutable `SalesLine` tax snapshots
- `SalesHeader` VAT and tax-category summaries
- Store VAT-registration snapshot
- Customer VAT number and address snapshot
- Non-VAT registered store sales recorded with no output VAT
- Service checkout persistence without stock batches or inventory transactions
- Existing Stock Item batch validation and stock deduction retained
- Automated sales VAT calculation checks

## Authoritative behavior

### VAT-registered store

For normal Stock Item and Service sale lines:

1. Resolve the item's approved Tax Category.
2. Resolve the effective rate using the sale date.
3. Treat the selling price as VAT inclusive.
4. Apply the line discount before VAT extraction.
5. Save the category, code, rate, inclusive flag, taxable value, VAT, and inclusive value.
6. Save header totals by Standard, Zero Rated, Exempt, and Out of Scope treatment.

### Non-VAT registered store

The sale does not collect output VAT.

Normal sale lines receive an immutable non-VAT / out-of-scope transaction snapshot with:

- zero VAT
- the full post-discount selling value as Out of Scope
- `IsVatRegisteredSale = false`

The item's Item Master tax assignment is not changed.

## Service behavior

The repository can now persist a Service sale line with:

- an Item Variant
- no Item Batch
- no stock validation
- no stock deduction
- no Inventory Transaction
- optional standard cost from `ItemVariant.CostPrice` or `AverageCost`
- the same discount, minimum-price, profit, and tax snapshot controls used by normal sales

The Cashier search and cart UI remain unchanged in this phase. Services become selectable in Phase 7E2.

## Special lines intentionally unresolved

The approved VAT treatment for these features still requires a separate decision:

- gift-voucher sale
- free issue
- supplier-funded free item

Those lines remain `LegacyUnknown` for tax amounts. Their existing operational behavior is preserved.

When a sale contains one of those unresolved special lines, the header tax summary also remains `LegacyUnknown` rather than publishing a partial VAT summary.

## Files

- `POS.Core/Services/Tax/SalesTaxService.cs`
- `POS.Core/Repositories/SalesRepository.cs`
- `POS.Core.CalculationTests/Program.cs`

## Automated checks

Six checks are added:

1. VAT-inclusive Standard VAT sale
2. VAT-inclusive line discount
3. Mixed categories with invoice-discount allocation
4. Non-VAT store sale
5. Service effective tax-profile resolution
6. Invoice-discount allocation reconciliation

Together with the existing twelve checks, the calculation project should report eighteen passing checks.

## Not included

- Cashier Service search or cart UI
- receipt VAT summary
- formal Tax Invoice numbering or printing
- customer return / credit-note VAT reversal
- supplier return VAT reversal
- VAT reports
- final gift-voucher or free-issue VAT treatment
