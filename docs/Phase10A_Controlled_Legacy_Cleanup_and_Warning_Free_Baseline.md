# Phase 10A — Controlled Legacy Cleanup and Warning-Free Build Baseline

## Baseline

- Branch basis: `sanjeewadev`
- Parent commit: `8f55481df7d0b16950aa184d59f17decd6937834`
- Database migration: none
- License Generator changes: none
- Existing automated regression registrations: 230

## Purpose

Phase 10A removes source artifacts that are unreachable from the active applications, removes one unused legacy UI dependency, and corrects the known application compiler warnings without changing business rules or transaction behavior.

This phase does not introduce a feature, database schema change, installer, CI workflow, cloud synchronization implementation, or UI redesign.

## Removed BackOffice artifacts

The following hidden Receipt Ledger implementation was not referenced by active navigation, dependency injection, source code, or XAML and has been removed:

- `ViewModels/ReceiptLedgerViewModel.cs`
- `Views/Pages/Sales/ReceiptLedgerView.xaml`
- `Views/Pages/Sales/ReceiptLedgerView.xaml.cs`

The unused `InverseBooleanConverter` was also removed. No active XAML resource or binding referenced it.

## Removed Cashier artifacts

The following legacy windows and dialogs had no active source, XAML, DI, or runtime construction references and have been removed:

- old Batch Selection dialog and its unused ViewModel;
- old Confirmation dialog;
- old Credit Note master window;
- old Discount dialog;
- old Price Discount dialog;
- old Price Override dialog;
- old Suspended Carts dialog;
- old Terminal Locked window.

The active replacements remain unchanged:

- live batch and stock selection within the current Sales workflow;
- `DiscountRuleDialog` for the active discount workflow;
- `ReturnInvoiceDialog` and current customer-return workflow;
- `HoldRecallDialog` for persistent cart hold/recall;
- `LockScreenView` and `CashierLockService` for terminal locking;
- `ManagerAuthDialogView` for protected overrides.

## Removed inactive infrastructure

- Removed the fully commented `CloudSyncWorker.cs`, which contained no compiled implementation.
- Removed the unused `POS.Core.Interfaces.IReceiptPrinterService` abstraction.
- Removed the unused `POS.Hardware.Services.ReceiptPrinterService` implementation.

The active Cashier printing and hardware paths remain:

- `POS.Cashier.UI.Services.IReceiptPrintService`;
- `EscPosReceiptPrintService`;
- `TerminalHardwareService`;
- `CashDrawerAuditService`;
- active receipt, Tax Invoice, Credit Note, shift-report, and drawer workflows.

## Project and dependency cleanup

- Removed unused `LiveCharts.Wpf` from BackOffice.
- Removed the corresponding `NU1701` suppression.
- Removed stale Cashier `Compile Remove` entries for voucher ViewModels that do not exist.
- Retained `PDFsharp-MigraDoc 6.2.4` and all active Phase 9B export dependencies.
- Retained `System.Drawing.Common` because active terminal-printer discovery and barcode printing still use it.

## Warning corrections

The known active application warnings were corrected as follows:

- Stock adjustment batch lookup now maps a missing batch number to an empty display value.
- Gift Voucher sale and redemption validation now returns a validated positive voucher ID instead of dereferencing nullable IDs after a separate check.
- Tender numpad `ClearValue` was renamed to `ClearText` to avoid hiding `DependencyObject.ClearValue`.
- Shift menu actions resolve a validated service provider once per operation.
- GRN matrix expiry propagation now pattern-matches the optional date before assignment.
- Subcategory parent-code normalization uses the already normalized non-null selected code.
- Warnings originating from the removed legacy Credit Note and receipt-printer implementations no longer compile into the solution.

These changes preserve the established Sri Lankan one-time Gift Voucher model and all existing stock, VAT, checkout, shift, return, credit, Free Issue, and export rules.

## Required validation

The Windows validation gate must confirm:

1. package restore succeeds without `NU1701`;
2. Debug build: zero errors and zero C# warnings;
3. all 230 automated regression checks pass;
4. Release build: zero errors and zero C# warnings;
5. no migration or License Generator file is changed;
6. removed type names and `LiveCharts` do not remain in active source/project files;
7. active receipt printing, cash drawer, terminal lock, discount, return, hold/recall, and export workflows remain operational;
8. the repository remains clean after commit, fast-forward merge, and push.

## Phase boundary

CI configuration, semantic versioning, deterministic publish profiles, release packaging, and installation validation remain deferred to Phase 10B.
