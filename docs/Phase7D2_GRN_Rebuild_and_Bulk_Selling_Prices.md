# Phase 7D2 — GRN Rebuild and Bulk Selling-Price Updates

Date: 11 July 2026

## Scope

Phase 7D2 rebuilds the Goods Received Note workflow around the approved Stock Item, Service and purchasing-VAT architecture.

This phase does not change Cashier sales VAT, customer returns, formal tax-invoice printing or the Purchase Order page.

## GRN rules

- GRN accepts Stock Items only.
- Direct GRN remains supported; a Purchase Order is optional.
- Supplier, supplier invoice number and document dates are confirmed before direct item entry.
- One document-level option defines whether supplier prices include VAT.
- Every line resolves its authoritative tax category and effective rate from the Tax Master using the supplier invoice date.
- Services remain excluded from GRN and all inventory receiving.
- Posting remains transactional and creates inventory, item batches or GENERAL stock buckets, PO receiving updates and supplier-ledger entries.

## Authoritative VAT preview

The page now uses the Phase 7D1 `PurchasingTaxService` for live preview and posting.

The preview displays and reconciles:

- line gross value;
- line discount;
- proportional document-discount allocation;
- tax-exclusive value;
- VAT amount;
- tax-inclusive line total;
- Standard VAT, Zero Rated, Exempt and Out of Scope summaries;
- tax-exclusive landed cost with freight allocation.

A preview calculation failure blocks posting. Historical posted GRNs remain unchanged.

Freight VAT treatment is not invented in this phase. Freight remains tax-unclassified while its value is allocated to landed cost.

## Selling-price update dialog

The GRN page no longer tries to fit retail and wholesale update controls into each matrix row.

`BULK SELLING PRICES...` opens a preview dialog for the GRN rows. Each row can be selected or excluded.

Retail and wholesale prices are controlled independently with these methods:

- Keep Current;
- Set Exact Price;
- Markup % from Landed Cost;
- Change Current Price by %.

Final VAT-inclusive selling prices may be rounded using:

- No Rounding;
- Nearest Rs. 1;
- Nearest Rs. 5;
- Nearest Rs. 10.

For landed-cost markup, the calculation is:

1. start from tax-exclusive landed cost;
2. apply the selected markup;
3. add the item’s effective VAT rate;
4. apply final-price rounding.

The dialog changes only proposed values in the draft GRN. It does not immediately change Item Master prices.

## Posting and audit

Selling prices change only when the GRN is posted.

- No price changes are selected by default.
- Posting states how many retail and wholesale prices will change.
- A zero or negative changed selling price is blocked.
- A wholesale price above retail produces a warning in the dialog.
- Conflicting proposed prices for duplicate rows of the same variant are blocked.
- Each actual variant price change creates a `PriceChangeHistory` record linked to the source GRN header and line.
- Old and new retail, wholesale, minimum and maximum values are retained in the audit record.
- Existing completed sales remain unchanged.

Cancelling or reversing a GRN must not silently restore old selling prices. Any future reversal design must use explicit, audited price decisions.

## UI design

The page uses the compact classic layout:

1. Document Setup
2. Stock Item / Variant Entry
3. GRN Lines, VAT and Proposed Selling Prices
4. Document Adjustments, VAT Summary, Totals and Posting

Retail and wholesale values are read-only in the main line grid. Proposed changes are made through the dedicated dialog.

## Automated checks

The calculation test project now includes checks for:

- VAT-inclusive markup from tax-exclusive landed cost;
- exact selling price;
- percentage change from current price;
- final-price rounding;
- keep-current behavior.

These are added to the seven Phase 7D1 purchasing VAT checks.

## Compatibility

No database migration is required. Phase 7B already created the GRN tax-snapshot and price-history source fields.
