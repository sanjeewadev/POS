# Phase 9B — Selective Exports, Dashboard and UI Finalization

## Approved scope

Phase 9B completes the approved selective export, dashboard and final UI work on top of Phase 9A commit `f9598e223e8c16eb05b854eed92a8a6302fdf7dd`.

The user explicitly excluded **GRN PDF downloading/export**. The GRN Export PDF button and its placeholder command are therefore removed. No GRN PDF builder, service action or automated PDF test is included.

No database migration is required or included.

## Export foundation

- Adds `PDFsharp-MigraDoc` 6.2.4 to `POS.Core`.
- Adds a shared MigraDoc/PDFsharp PDF service for text and tabular documents.
- Adds repeated table headings, page numbers, store/document headings, generated-by details and safe multi-page output.
- Adds a shared UTF-8 BOM CSV service with correct quoting for commas, quotes, line breaks and Unicode text.
- Adds safe export filename handling.
- Adds one export authorization service:
  - operational documents require a signed-in user;
  - bulk or financially sensitive exports require Manager or Administrator.
- Empty exports are rejected before file creation.

## PDF outputs

The following PDFs are implemented:

1. Purchase Order.
2. Cashier Sales Receipt.
3. Cashier Tax Invoice.
4. Cashier Customer Credit Note preview.
5. BackOffice Customer Credit Note.
6. Supplier Debit Note.
7. Customer Account Statement.
8. Customer Payment Receipt.
9. Supplier Claim Statement.
10. Financial Summary.
11. Item Sales Analysis.
12. Supplier Summary.
13. VAT Operational Summary.

Completed transaction documents use the saved document text/snapshots. Current item prices, current VAT rates and live master-data changes do not replace historical values.

## CSV outputs

- Stock Balance.
- Item Sales Analysis.
- Supplier Summary.
- VAT document and reconciliation detail.
- Supplier Claim detail.

The Stock Balance action is now accurately named CSV and uses the shared formatter/writer. Existing unrelated working exports remain unchanged.

## Dashboard

The static Dashboard is replaced with a compact operational view containing:

- merchandise sales, returns, net sales and gross profit for a selected period;
- tender totals;
- customer-credit outstanding;
- supplier outstanding;
- low and negative Stock Item counts;
- open shifts and held carts;
- Draft and Submitted Supplier Claim counts;
- top five items/services;
- operational attention rows.

Services are explicitly excluded from stock-alert counts.

## Cashier finalization

- Removes the unfinished Reports menu item.
- Hides the unsupported Credit Note tender.
- Renames the handler used by working Cheque and Gift Voucher tenders so it no longer implies those tenders are unsupported.
- Adds Save PDF to the existing receipt, Tax Invoice and Credit Note preview dialog.
- Keeps the preview open after PDF save and preserves the existing return-to-terminal-focus workflow when it closes.

## Navigation and UI cleanup

- Renames `Purchase Order DB` to `Purchase Order History`.
- Removes the GRN PDF placeholder because GRN PDF export is excluded.
- Adds compact export buttons to the approved pages.
- Uses existing shared brushes and `LightSelectionBrush` on the Dashboard.
- Does not merge the approved separate operational pages.
- Keeps Receipt Ledger hidden.

## Automated coverage

Sixteen Phase 9B checks are registered, increasing the full regression count from 214 to 230. They cover:

- CSV escaping, Unicode and row validation;
- safe filenames;
- saved Purchase Order values and explicit GRN PDF exclusion;
- Stock Item/Service export behavior;
- Gift Voucher separation and returned COGS presentation;
- supplier, VAT and claim output projections;
- customer payment receipt allocation reconciliation;
- Stock Balance shared CSV formatting;
- valid text and multi-page PDF structures;
- export role authorization;
- Dashboard stock-alert logic excluding Services;
- empty-export safety.

## Runtime verification

Before commit, generate each approved PDF and CSV once, open representative files, reconcile important totals with their source pages, test Manager/Admin denial behavior, verify Dashboard counts against known data, and confirm Cashier tender/navigation cleanup.
