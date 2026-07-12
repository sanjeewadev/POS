# Phase 7F1 — Customer Returns and Credit Notes

## Scope

This phase implements receipt-linked customer returns for normal Stock Items and Services.

Supported behavior:

- exact completed-invoice lookup;
- full and partial returns;
- repeated-return and over-return prevention;
- original Stock Item batch restoration;
- Service return without inventory activity;
- proportional reversal of original prices, discounts and tax snapshots;
- exact final residual reconciliation after multiple partial returns;
- immutable CustomerReturnHeader and CustomerReturnLine snapshots;
- Cash refund through a Paid Out cash movement;
- formal Credit Note preview and printing;
- atomic database transaction covering return, inventory and cash movement.

## Exclusions

This phase does not implement:

- blind or no-invoice returns;
- editable historical selling prices;
- gift-voucher line returns;
- free-item promotional returns;
- damaged/quarantine inventory;
- card, bank, split or store-credit refunds;
- supplier returns;
- recalculation using current Tax Master data.

## Tax and monetary rules

- All return values come from the original SalesLine snapshots.
- Current Item Master prices and current VAT rates are not used.
- Ordinary partial returns use proportional two-decimal allocation.
- The final remaining return uses exact unreturned residuals.
- LegacyUnknown returns preserve the financial refund but do not invent VAT.

## Inventory rules

- Stock Items return to the exact original ItemBatch.
- The batch active/deactivated/expiry state is not changed.
- A positive RETURN InventoryTransaction is created.
- Services create no inventory transaction and use no batch.

## Settlement

The initial approved settlement method is Cash only.

Every completed return creates:

- one CustomerReturnHeader;
- one or more CustomerReturnLine records;
- one Credit Note number;
- one Paid Out CashMovement;
- inventory restoration only for Stock Items.

## Database impact

No migration is required. Existing customer-return, inventory, cash-movement and document-sequence tables are used.
