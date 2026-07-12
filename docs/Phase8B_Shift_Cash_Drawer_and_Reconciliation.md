# Phase 8B — Shift, Cash, Drawer and Reconciliation

## Purpose

Phase 8B completes the daily Cashier shift cycle and makes drawer cash auditable from shift opening through the immutable Z close.

## Delivered scope

- Opening cash is entered when a shift starts and is never rewritten by later Float In or Float Out actions.
- One Open or Closing shift is allowed per terminal.
- Cash summaries use completed `SalesPayments` tender rows, including the Cash portion of split payments.
- Card, Cheque, Gift Voucher, Customer Credit and other non-cash tenders do not change expected drawer cash.
- Paid In, Paid Out, Float In, Float Out and cash refunds are stored as individually numbered cash movements.
- Paid Out is blocked when it exceeds calculated drawer cash.
- X Reports calculate live totals without closing the shift.
- Shift closing uses a blind denomination count, blocks Active/Held carts, requires manager approval for a non-zero variance, creates one immutable Z snapshot and is idempotent.
- Closed shifts reject new checkout attempts.
- Automatic cash-sale drawer opening, Paid In/Out drawer opening, Float In/Out, No Sale and manual drawer opening are audited, including hardware failures.
- BackOffice Cash Movement loads real movement rows and supports focused CSV export.
- BackOffice Float Cash Log uses live summaries for open shifts and immutable Z snapshots for closed shifts.

## Authoritative expected-cash formula

```text
Opening Cash
+ completed Cash tender amounts
+ Paid In
+ Float In
- Paid Out
- Float Out
- Cash customer refunds
= Expected Drawer Cash
```

`TenderedAmount` and `ChangeAmount` remain payment-audit values. Expected drawer cash uses the completed Cash payment `Amount`, so tender and change cannot be counted twice.

## Migration

`20260712200000_AddShiftCashDrawerAndReconciliation`

The additive migration introduces:

- `ShiftCloseSnapshots`
- `CashDrawerEvents`
- one-open-shift-per-terminal database protection
- Paid In, Paid Out and Z Report document sequences
- shift and cash-movement query indexes
- one-time normalization of historical Float In/Out rows that previously changed both opening cash and cash movements
- normalization of legacy terminal/status text; if old data contains duplicate open shifts for one terminal, the newest is retained and older invalid rows are closed before the unique index is created

No earlier migration is edited.

## BackOffice pages

The existing separate pages remain:

- Cash Movement — individual drawer movements and CSV export
- Float Cash Log — shift reconciliation, tender totals, Z number, variance and ledger drill-down, including drawer-open success/failure events

No additional BackOffice page is introduced.

## Automated checks

Phase 8B adds 24 checks to the existing 100-check suite, for an expected total of 124. Coverage includes:

- shift uniqueness and opening cash
- Float In/Out integrity
- cash-movement numbering
- cash, card, cheque and split tenders
- tender/change handling
- Paid In/Out and cash refunds
- negative drawer prevention
- X and Z report behavior
- Active/Held cart close blocking
- variance approval
- duplicate-close idempotency
- closed-shift checkout rejection
- drawer success/failure audit

## Deferred scope

This phase does not implement Customer Credit ledger completion, Gift Voucher lifecycle completion, Free Issue claims, Dashboard redesign, installer work or broad export formats. Those remain in later approved phases.
