# Phase 7C2 — Item Master for Stock Items and Services

Date: 2026-07-11

## Scope

This phase rebuilds Item Master around the approved item and VAT architecture.

It does not yet implement service sales in Cashier or the final PO/GRN VAT calculation engine.

## Authoritative item types

Item Master exposes only:

- `StockItem`
- `Service`

### Stock Item

A Stock Item:

- tracks inventory;
- may use batch/expiry tracking or the existing average-cost `GENERAL` bucket;
- may be assigned approved suppliers;
- may appear in PO and GRN;
- may appear in stock count and stock adjustment;
- keeps the existing stock and variant architecture.

### Service

A Service:

- has no stock;
- has no batch or expiry tracking;
- has no reorder level;
- cannot be assigned as inventory to a supplier;
- cannot appear in PO or GRN;
- does not appear in current Cashier item search until the later Cashier service integration;
- may have Standard or matrix variants;
- may have a barcode, quantity, standard cost, retail price, wholesale price, minimum price, maximum price, and tax category.

## Tax assignment

Item Master now selects the stable `TaxCategory` rather than a rate-code string.

Allowed categories:

- Standard VAT
- Zero Rated
- Exempt
- Out of Scope

For compatibility while PO/GRN and Cashier are migrated:

- Standard VAT stores the currently effective Standard VAT code in legacy `TaxCode`.
- Fixed 0% treatments store `TAX-FREE` in legacy `TaxCode`.
- `ItemParent.TaxCategoryId` is authoritative.

The system blocks saving a Standard VAT item if no Standard VAT rate is effective today.

## Selling prices

Retail, wholesale, minimum, and maximum selling prices are stored VAT inclusive.

The old Item Master tax-inclusive toggle is removed. Purchase VAT-inclusive/exclusive entry mode belongs to the future PO/GRN document workflow.

## History safety

Before history exists, an unused saved item may still correct:

- category;
- optional sub-category;
- item type;
- Unit of Measure;
- stock tracking;
- expiry tracking;
- scale setting;
- variant matrix.

After stock, batch, PO, GRN, sale, return, or adjustment history exists, those structural fields are locked.

The following remain editable for future transactions:

- item and print names;
- tax category;
- selling prices;
- optional standard/current cost;
- cashier discount permission;
- purchase/sale locks;
- supplier information for Stock Items.

Item code remains immutable after the first save.

## Downstream safety

Until later integration phases:

- PO item lookup excludes Services.
- GRN item lookup excludes Services.
- PO save validation rejects Services.
- GRN save validation rejects Services.
- current Cashier item lookup excludes Services;
- current Express Item assignment excludes Services;
- Stock Balance excludes Services;
- Stock Adjustment lookup and posting reject Services.

This prevents a Service from entering old stock-only transaction logic.

## Database

No migration is included. Phase 7B1 already created:

- `ItemParent.ItemType`
- `ItemParent.TaxCategoryId`
- `TaxCategory`
- effective-dated `TaxRate`

## Required smoke tests

1. Existing Stock Item opens and saves normally.
2. Existing item with history shows structure locked.
3. Unused saved item allows structural correction.
4. New Stock Item can be created with Standard VAT.
5. New average-cost Stock Item can be created with Batch Tracking unticked.
6. New Service can be created without stock flags or supplier assignment.
7. Service is absent from PO lookup.
8. Service is absent from GRN lookup.
9. Service is absent from current Cashier and Express Item lookup.
10. Service is absent from Stock Balance and Stock Adjustment.
11. Zero Rated, Exempt, and Out of Scope remain separate selections.
12. Duplicate item code, SKU, and barcode validation remains active.
13. Existing item IDs, variants, batches, stock, and transaction history remain unchanged.
