# Phase 9A — BackOffice Operational Page Completion

## Baseline

- Branch: `sanjeewadev`
- Baseline commit: `12f8d5f2cee7076d053318d22ec331b81ee1b0ee`
- Prior completed phase: Phase 8E — Free Issue Rules and Supplier Claims

## Scope completed

Phase 9A completes the retained operational BackOffice pages without beginning Phase 9B export or dashboard work.

### Customer Return

- Read-only Credit Note and customer-return history.
- Date and text filters.
- Original invoice, customer, cashier, terminal, refund method and tax totals.
- Returned-line detail with immutable tax snapshots.
- Credit Note preview, copy and standard Windows print action.

### Suspended Transactions

- Reads the persistent Phase 8A cart-session lifecycle.
- Active, Held, Completed and Cancelled status filters.
- Terminal, cashier, shift, customer, totals and lifecycle timestamps.
- Read-only line details and completed-invoice/cancellation information.
- No unsafe Force Void action.

### Item Sales Analysis

- Stock Items and Services are both supported.
- Category, item, SKU and item-type search.
- Sold, returned and net quantity/value/cost/profit.
- Current stock only for Stock Items.
- Last sale, top sellers and slow/non-selling Stock Items.
- Read-only transaction drill-down.

The approved period-activity basis is used:

```text
completed sales in the selected period
minus
customer returns processed in the selected period
```

Sold, returned and net values remain visible separately.

### Sales Explorer

- Authoritative completed-sales investigation page.
- Invoice, customer, cashier, terminal, date and return-status filters.
- Merchandise revenue excludes Gift Voucher issue value.
- Lines, discounts, immutable VAT snapshots and payments.
- Tax Invoice details, related Credit Notes and print/reprint/failure audit.
- Actual Not Returned, Partially Returned and Fully Returned status.
- The obsolete Receipt Ledger navigation route is hidden; its source files remain for controlled cleanup in Phase 10.

### Security Audit

- Read-only chronological view from existing operational tables.
- Cart holds, recalls, completions and cancellations.
- Customer returns, manual discounts, price overrides and Free Issues.
- No Sale and failed drawer events.
- Failed logins and manager-approval audit events.
- Document reprints and print failures.
- Cashier activity summaries use neutral review wording.

### Financial Summary

- Merchandise sales exclude Gift Voucher issuance.
- Customer returns reduce sales.
- Returned cost reverses COGS.
- Tender totals are shown.
- Posted purchases and supplier returns are shown.
- Paid In, ordinary Paid Out, Float In, Float Out and customer cash refunds are separated.
- The page is explicitly operational and not a full accounting profit-and-loss statement.

### Supplier Reports

- Supplier search and date filters.
- Outstanding balances.
- Purchase volume and ranking.
- Posted supplier-return quantity and value summaries.
- Compact loading, empty and error states.

## UI standard

All touched pages use the existing compact business-software style and reuse `LightSelectionBrush` for selected DataGrid cells. Oversized cards, placeholder charts and fake export actions were removed. Actual export work remains in Phase 9B.

## Database impact

No database migration is required or included. Phase 9A reads existing Phase 7–8E operational and audit data.

## Automated coverage

The custom regression runner increases from 202 to 214 registered checks. New temporary-SQLite tests cover:

- customer-return history, filters, details and tax snapshots;
- persistent-cart statuses and read-only line details;
- Stock Item and Service analytics;
- period-activity return calculations;
- Gift Voucher revenue exclusion in Sales Explorer;
- Sales Explorer tax, tender, Credit Note and document-audit details;
- returned COGS reversal;
- cash-movement classification and tender totals;
- security-event aggregation and search;
- supplier filtering and financial reconciliation;
- invalid date-range rejection.

## Runtime gate

1. Customer Return history and Credit Note preview/print.
2. Suspended Transactions filters and read-only details.
3. Item Sales Analysis for a Stock Item and a Service.
4. Sales Explorer payment, VAT, Credit Note and print-audit details.
5. Security Audit representative events.
6. Financial Summary reconciliation.
7. Supplier Reports filters and ranking.
8. Receipt Ledger route hidden.
9. Compact layouts and light-blue row selection.
