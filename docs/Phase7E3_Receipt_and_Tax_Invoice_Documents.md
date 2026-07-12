# Phase 7E3 — Receipt and Tax Invoice Documents

## Scope

Phase 7E3 connects the completed-sale tax snapshots from Phase 7E2 to ordinary sales receipts and formal Tax Invoices.

The implementation preserves the existing checkout transaction, payment processing, invoice numbering, stock deduction, batch deduction, and cash-drawer flow.

## Ordinary sales receipt

The receipt is explicitly identified as:

- `SALES RECEIPT`
- `NOT A TAX INVOICE`
- `ORIGINAL` or `REPRINT`

It includes:

- store identity;
- invoice number and transaction date;
- terminal and cashier;
- customer name;
- Stock Item and Service lines;
- quantities, VAT-inclusive prices, discounts and line totals;
- payment lines, tendered amount and change;
- Standard VAT taxable value and VAT;
- Zero Rated, Exempt and Out of Scope totals.

Legacy transactions whose tax snapshot status is `LegacyUnknown` print a clear notice instead of receiving reconstructed VAT values.

## Formal Tax Invoice

A formal Tax Invoice is a separate document linked to the completed sale. It contains:

- a unique Tax Invoice number;
- the original receipt number;
- issue and transaction timestamps;
- saved supplier TIN and VAT registration snapshots;
- confirmed customer name, address, TIN and VAT number;
- saved line-level Tax Category, VAT rate, taxable value and VAT amount;
- saved header tax totals;
- `ORIGINAL` or `REPRINT` identification.

Tax Invoice formatting uses only immutable sale snapshots. It does not read the current Tax Master rate to recalculate an older sale.

Issuing is blocked when:

- the sale is voided or incomplete;
- the store was not VAT registered for the sale;
- the sale or any line has incomplete/legacy tax snapshots;
- saved supplier VAT information is missing;
- required customer tax details are not supplied.

Repeated issue requests return the existing Tax Invoice number and do not consume another sequence number.

## Printing and preview

Cashier Print Options now provides:

- Last Receipt;
- Tax Invoice;
- Quotation.

Receipt and Tax Invoice documents are previewed before manual printing. Existing terminal receipt-printer and paper-width settings are reused.

A printer failure occurs after the completed sale is loaded and cannot create a duplicate sale.

## Audit migration

Migration `20260712080000_AddSalesDocumentAudit` adds `SalesDocumentAudits` and a filtered unique index for non-null Tax Invoice numbers.

Audited events include:

- Tax Invoice issue;
- original successful print;
- successful reprint;
- failed print attempt.

Each audit records the sale, document type and number, copy number, user, terminal, printer, UTC timestamp, success status and error text.

The migration is additive. It does not reset the database or invent values for historical transactions.

## Verification

Automated checks cover:

- receipt and Tax Invoice separation;
- 58 mm wrapping and long descriptions;
- Stock Item and Service document lines;
- payment and tax summaries;
- immutable tax snapshot formatting;
- Tax Invoice idempotence and uniqueness;
- invalid Tax Invoice issue prevention;
- original, reprint and failure audits;
- terminal-scoped last-receipt loading.

Required runtime smoke tests:

1. Preview and print an ordinary receipt.
2. Reprint it and confirm `REPRINT`.
3. Issue a Tax Invoice using customer tax details.
4. Preview and print it.
5. Reprint it and confirm the same Tax Invoice number with `REPRINT`.
6. Disable/unavailable the printer and confirm the sale is not duplicated.
7. Confirm a `LegacyUnknown` sale cannot be issued as a Tax Invoice.
