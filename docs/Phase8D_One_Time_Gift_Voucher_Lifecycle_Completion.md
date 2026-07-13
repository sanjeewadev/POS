# Phase 8D — One-Time Gift Voucher Lifecycle Completion

## Approved model

Phase 8D keeps the Sri Lankan one-time gift voucher workflow already used by the project.

- A voucher is generated in `Created` status.
- It becomes `Active` only when its sale commits successfully.
- It can be redeemed once.
- The amount applied plus any forfeited amount must equal the original face value.
- A manager or administrator must authenticate when unused value will be forfeited.
- A redeemed voucher cannot be reused, reactivated, topped up, or exchanged for cash.
- Gift Voucher cannot be used to purchase another Gift Voucher.

Partial-balance and repeated-redemption vouchers are intentionally outside the approved scope.

## Lifecycle and audit

Supported operational statuses are:

- `Created`
- `Active`
- `Redeemed`
- `Blocked`
- `Expired`
- `Voided`

Legacy `Cancelled` rows are presented and migrated as `Voided`.

The immutable movement history records generation, original printing, reprinting, activation, redemption, blocking, unblocking, voiding, and replacement-voucher issue. Financial movements retain invoice, payment, return, cashier, terminal, authorization, and idempotency references.

## Checkout safety

Voucher activation and redemption occur within the sale transaction.

Redemption is revalidated at checkout and uses a conditional status update so concurrent or repeated use is rejected. The permanent payment row stores the voucher face value, applied amount, forfeited amount, voucher identity, and approving manager when applicable. Existing checkout-token idempotency returns the original completed sale without posting a second voucher movement.

## Returns

Merchandise or services originally funded by a redeemed voucher do not reactivate the old voucher.

The return workflow issues a new one-time replacement voucher for the eligible voucher-funded portion. Any remaining return amount follows the other original tender settlement, including Cash where applicable. Prior returns are considered so replacement-voucher value cannot exceed the original Gift Voucher payment amount and the same return cannot issue value twice.

Gift Voucher sale lines themselves remain non-returnable in the normal merchandise-return workflow.

## VAT and revenue separation

Voucher issue value is stored separately in `SalesHeaders.GiftVoucherIssueTotal`.

- Voucher-only issue receipts have a complete zero-VAT snapshot.
- Voucher-only receipts cannot be formatted as Tax Invoices.
- Mixed merchandise and voucher-issue sales preserve VAT snapshots for the merchandise or service lines.
- Tax Invoice output identifies voucher issue value as excluded from VAT taxable supplies.
- VAT reports exclude voucher-only issue value.
- Sales revenue summaries exclude voucher issue value while retaining the operational receipt.
- VAT continues to be calculated on the goods or services supplied when the voucher is redeemed.

This operational policy should be confirmed with the store's accountant before production deployment.

## BackOffice completion

The Gift Voucher page now provides:

- batch generation;
- status and text filters;
- face value, redeemed value, forfeited value, issue and redemption references;
- immutable transaction history;
- original print and reprint through the Windows print workflow;
- CSV register export;
- Block, Unblock, and Void actions with entered reason;
- manager or administrator authorization;
- real logged-in audit username.

Only an unsold `Created` voucher can be voided. An issued `Active` voucher must be blocked instead.

## Additive migration

Migration:

`20260713000000_AddOneTimeGiftVoucherLifecycleCompletion`

It adds voucher print audit data, manager authorization, transaction reference links, separate voucher-issue totals, and return replacement-voucher fields. Existing migrations are unchanged.

## Automated coverage

Phase 8D adds 25 checks to the 152-check baseline, producing 177 registered checks. Coverage includes unique generation, atomic activation, one-time redemption, forfeiture authorization, status controls, print audit, VAT and revenue separation, checkout idempotency, replacement vouchers on returns, repeated-return ceilings, Tax Invoice behavior, and legacy status compatibility.
