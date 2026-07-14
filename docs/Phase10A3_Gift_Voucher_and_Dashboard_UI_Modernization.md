# Phase 10A.3 — Gift Voucher and Dashboard UI Modernization

## Purpose

This phase modernizes two remaining BackOffice pages while preserving the approved compact, classic business-software style introduced by Phase 10A.2.

## Gift Voucher Management

- Replaces the oversized legacy title band with the standard compact page heading.
- Reorganizes voucher-batch creation into two readable rows.
- Uses the shared BackOffice input, button, panel, filter and DataGrid styles.
- Reduces the voucher register to high-value columns and moves financial details into a selected-voucher summary.
- Adds readable status badges for Created, Active, Redeemed, Expired, Blocked and Voided states.
- Shows only lifecycle actions valid for the voucher's current state:
  - Block for Active vouchers.
  - Unblock for Blocked vouchers.
  - Void for Created vouchers.
- Preserves Print for all listed vouchers.
- Adds a resizable split between the voucher register and immutable movement history.
- Replaces the bright full-width status strip with a compact status footer.

The approved one-time voucher lifecycle, commands, repository operations, printing, authorization and CSV export behavior are unchanged.

## Dashboard

- Uses a cleaner page heading and compact period filter bar.
- Groups financial and operational totals into restrained summary panels.
- Keeps sales, returns, net sales, gross profit, customer credit, supplier balances, stock attention and open operations visible without oversized cards.
- Reorganizes tender totals, top items/services and operational attention into consistent bordered panels.
- Adds resizable lower dashboard columns.
- Uses the shared BackOffice DataGrid and selection styles.

All dashboard repository calculations remain unchanged, including the exclusion of Service items from stock alerts.

## Scope boundaries

- No database migration.
- No entity, repository or calculation change.
- No Cashier UI change.
- No License Generator change.
- No Gift Voucher lifecycle or authorization change.
- No Dashboard data-definition change.
