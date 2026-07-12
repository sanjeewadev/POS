# Phase 8A — Cart Lifecycle and Checkout Safety

## Status

Implementation basis for the Phase 8A working branch.

## Scope

Phase 8A adds persistent cashier-cart recovery and checkout idempotency without changing the approved VAT, pricing, inventory, return, receipt or Tax Invoice calculations.

## Persistent cart lifecycle

Each cashier cart has one stable GUID token and one lifecycle status:

- `Active`
- `Held`
- `Completed`
- `Cancelled`

A cart belongs to one terminal, open shift and cashier. Only one active cart is allowed for that owner, while multiple held carts are allowed.

The saved cart restores:

- Stock Items and Services;
- exact batch selection;
- customer snapshot and Retail/Wholesale mode;
- selected prices and quantities;
- manual line discounts and invoice discount;
- price override and approving user snapshot;
- Gift Voucher sale lines;
- Free Issue and supplier-recovery snapshots;
- existing Discount Rule snapshot data for compatibility only.

Suspending a cart does not reserve stock. Final checkout continues to validate current item status, batch stock, expiry, customer state and effective VAT rules.

## Payment data

Unposted tender details are deliberately not stored in active or held carts. After recovery or Recall, payment must be entered again.

Completed-sale payment details are permanently stored in `SalesPayments` and shown in Sales Explorer. The saved audit includes:

- payment type and applied amount;
- cash tendered amount and change;
- card provider/type, last six digits and terminal/reference number;
- cheque reference, bank/branch and payment date;
- Gift Voucher reference;
- cashier and terminal.

Full card numbers, CVV, PIN and magnetic-stripe data are never stored.

## Cancellation

Cancelling an active or held cart requires a reason. The cart snapshot, cashier, terminal, shift, value, timestamp and cancellation reason remain available for audit. Cancellation creates no invoice, payment, stock movement, voucher transaction or ledger entry.

## Checkout idempotency

`SalesHeaders.CheckoutToken` stores the cart token and has a filtered unique index. Repeating the same checkout request returns the already-committed sale instead of creating another invoice, payment or stock deduction.

The active cart is marked `Completed` and linked to the saved sale in the same EF Core transaction as the invoice, payments and inventory movements. A failed checkout leaves the cart active and recoverable.

## Manager authorization

Manager or Administrator credentials are validated through a separate approval method. Successful approval returns the approver identity but does not replace the active cashier in `AuthService.CurrentUser`.

## Database migration

Migration:

`20260712150000_AddCashierCartLifecycleAndCheckoutSafety`

Additions:

- `CashierCartSessions`
- `CashierCartLines`
- `SalesHeaders.CheckoutToken`
- permanent completed-payment audit fields on `SalesPayments`

No existing migration is modified.

## Automated validation

The calculation/repository suite is expanded from 78 to 100 checks. Phase 8A checks cover:

- Stock Item, Service and mixed-cart restoration;
- customer, Wholesale, batch, pricing and discount restoration;
- price-override, Gift Voucher sale and Free Issue snapshots;
- exclusion of payment drafts from held carts;
- restart recovery;
- Suspend, Recall, owner isolation and one-time Recall;
- cancellation audit with no financial posting;
- checkout idempotency and single stock/payment effects;
- failed-checkout recovery and successful cart completion;
- manager approval without cashier-session replacement;
- permanent completed-payment audit persistence.

## Excluded from Phase 8A

- shift closing, X/Z reports and drawer reconciliation;
- Customer Credit completion;
- Gift Voucher lifecycle expansion;
- Free Issue administration expansion;
- automatic Discount Rules development;
- BackOffice page redesign.
