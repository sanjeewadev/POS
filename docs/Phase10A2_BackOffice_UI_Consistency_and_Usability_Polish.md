# Phase 10A.2 — BackOffice UI Consistency and Usability Polish

## Baseline

- Branch: `sanjeewadev`
- Commit: `22a52f14e1690e1f499f2d0deaf48ad09a5bee01`
- Previous state: Phase 10A plus the global Boolean-to-visibility runtime hotfix
- Database migrations: none
- Cashier changes: none
- License Generator changes: none

## Purpose

This phase is a targeted BackOffice usability pass. It preserves the compact classic business-software style and avoids redesigning pages that were already acceptable. The work concentrates on shared visual consistency, permanently reachable primary actions, clearer page organization, readable selections, and safe behavior at smaller desktop resolutions.

## Shared UI foundation

`Resources/BackOfficeControls.xaml` defines reusable BackOffice styles for:

- section headings and field labels;
- help text;
- text, combo and date inputs;
- default, primary, success and destructive buttons;
- section panels, filter bars and action footers;
- read-only DataGrid rows, cells and headers;
- compact KPI panels;
- compact tab items and empty-state text.

The dictionary also standardizes active and inactive WPF selection brushes to a light teal background with black text. Existing editable-grid emphasis remains local where a page requires it.

## Targeted page corrections

### Supplier Master

- The editor and database panes are now separated by a resize splitter.
- The editor is vertically scrollable while its action footer stays fixed.
- Clear and Save Supplier remain permanently visible.
- Deactivate and Reactivate are mutually exclusive and appear only for an applicable saved supplier.
- Delete Unused remains separated as the destructive action.
- Supplier name and company columns use proportional widths to reduce clipping.
- Search actions use compact labels.

### Customer Master

- The customer database was reduced to operationally useful columns; less-used details remain available in the editor.
- The editor is organized into Basic Details, Business Details and Credit Settings tabs.
- Save Customer Profile and Clear remain fixed and reachable.
- Credit balance and available-credit information remain visible in the Credit Settings tab.
- Existing commands and bindings are preserved.

### Cash Movement Dashboard

- The oversized modern-card presentation was replaced with the compact BackOffice report style.
- KPI values, filter controls and report rows use the normal BackOffice sizing and palette.
- The export and filtering commands are unchanged.

### Free Issue Rule Setup

- Rule fields are grouped into Identity & Funding, Eligibility & Validity and Advanced Controls.
- The footer keeps rule lifecycle actions separate from New Rule and Save Rule.
- The rule database uses proportional columns and the shared selection style.
- A resize splitter separates the editor from the database.

### User Management

- The staff directory starts at a more appropriate width.
- A resize splitter allows the directory/editor balance to be adjusted.
- The existing identity, authentication and lifecycle workflow remains unchanged.

### Supplier Ledger

- The ledger and payment panel are separated by a resize splitter.
- The payment panel now has controlled minimum and maximum widths.
- The payment heading and outstanding amount are compacted to preserve working space.

## Supporting consistency corrections

- Read-only blue selection shades in operational BackOffice pages were normalized to the shared light-teal selection palette.
- Horizontal scrolling was enabled as a safety fallback on License Management, Store Settings, Terminal Settings and Price Change History rather than clipping content at narrower widths.

## Explicit exclusions

- no business-rule changes;
- no VAT, stock, payment, credit, voucher, return or Free Issue calculation changes;
- no ViewModel command behavior changes;
- no database migration;
- no Cashier UI change;
- no License Generator change;
- no release-packaging work.

## Required validation

1. Restore packages.
2. Build Debug with zero errors and zero C# warnings.
3. Run all 230 automated tests.
4. Build Release with zero errors and zero C# warnings.
5. Open every modified BackOffice page using the Bookshop Demo database.
6. Verify Supplier Master actions remain visible at 1366×768 and 1920×1080.
7. Verify Customer Master tabs preserve all profile and credit fields.
8. Verify Free Issue rule create/edit/activate workflows.
9. Verify Supplier Ledger payment entry and all ledger tabs.
10. Verify Cash Movement filters and export.
11. Verify selection remains readable in the adjusted operational grids.
12. Verify no migration or License Generator file is staged.
