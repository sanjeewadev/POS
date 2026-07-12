# Phase 8C — Customer Credit and Customer Ledger Completion

## Purpose

Phase 8C completes the Customer Credit workflow across Cashier, Customer Master data, Customer Ledger, customer payments, customer returns and financial audit records.

## Customer Credit sales

- Customer Credit is available only for an identified active customer.
- The customer must have credit enabled, `CreditStatus = Active`, no credit lock and a positive credit limit.
- Available credit is revalidated inside the checkout transaction.
- Full and split Customer Credit tenders are supported.
- Only the Customer Credit tender portion creates a receivable.
- The due date is calculated from the sale date and the customer's configured credit days.
- The completed sale, tender, receivable ledger debit, customer balance, stock, VAT and cart completion are committed atomically.
- Existing checkout-token idempotency prevents duplicate invoices and duplicate receivables.

## Customer payments

Supported receipt methods:

- Cash
- Card
- Cheque
- Bank Transfer

Payments are allocated to open invoices by oldest due date first. Overpayments and over-allocation are rejected for Version 1.0.

Each payment creates:

- a unique Customer Payment Receipt number (`CPR-000001` format);
- one Customer Ledger credit;
- one or more immutable invoice allocations;
- an updated customer balance.

Cash payments received through an open Cashier shift create one linked Paid In movement. Card, Cheque and Bank Transfer receipts do not affect drawer cash. BackOffice receipts require a destination account or counter reference.

## Customer returns against credit invoices

A return first reduces the remaining receivable on the original invoice. Any return value beyond the original invoice's remaining receivable becomes a Cash refund.

```text
Account credit = minimum(return total, original invoice outstanding)
Cash refund    = return total - account credit
```

This prevents a customer from receiving both a full Cash refund and a full account-balance reduction for the same return.

## BackOffice Customer Ledger

The existing Customer Ledger page now provides:

- current outstanding balance;
- credit limit and available credit;
- overdue amount and aging buckets;
- open invoices and due dates;
- debit, credit and running-balance statement rows;
- Receive Payment workflow;
- printable customer statement, including Microsoft Print to PDF.

Customer Master remains the authority for credit-enabled status, credit limit, credit days, credit status, credit lock and active/inactive state.

## Database migration

Migration:

`20260712220000_AddCustomerCreditAndLedgerCompletion`

The additive migration:

- extends `CustomerLedgers` with sale, return, receipt, due-date, allocation and status fields;
- adds `CustomerPaymentReceipts`;
- adds `CustomerLedgerAllocations`;
- stores account-credit and Cash-refund settlement amounts on customer returns;
- adds the `CPR` document sequence;
- adds filtered unique indexes that prevent duplicate sale, return and payment ledger posting.

No previous migration is edited.

## Automated checks

Phase 8C adds 28 automated checks, increasing the expected suite from 124 to 152. Coverage includes:

- credit eligibility, hold, lock and limit enforcement;
- full and split Customer Credit sales;
- due dates, balance updates and checkout idempotency;
- oldest-invoice, partial and full payment allocation;
- payment receipt idempotency and reference requirements;
- Cash Paid In and non-Cash drawer isolation;
- BackOffice destination validation;
- credit-sale returns and partial Cash refunds;
- duplicate-return protection;
- aging buckets, statement running balance and statement formatting.
