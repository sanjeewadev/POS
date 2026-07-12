# Phase 7E2 — Cashier Stock Item and Service VAT Integration

Date: 2026-07-12

## Purpose

Connect Cashier to the shared sales VAT engine while preserving the existing Stock Item batch, stock, payment, checkout, invoice-number and receipt paths.

## Included

- Cashier search, SKU and barcode support for Services
- Explicit Service cart path with no batch and no stock validation
- Mixed Stock Item and Service sales
- Existing Stock Item batch selection and inventory deduction preserved
- Effective-dated Tax Category and VAT-rate loading for Cashier items
- VAT-inclusive retail and wholesale calculations
- Live Standard VAT taxable amount and VAT total
- Live Zero Rated, Exempt and Out of Scope totals
- VAT recalculation after line discounts
- Fixed-LKR invoice discount entry
- Exact proportional invoice-discount allocation through `SalesTaxService`
- Repository-side authoritative recalculation and validation
- Immutable SalesHeader and SalesLine tax snapshots
- Automated calculation, search, persistence and inventory regression checks

## Invoice-discount persistence without a migration

No database migration is required.

The existing columns preserve the complete result:

- `SalesHeader.TotalDiscount` stores total line discounts plus the invoice discount.
- `SalesLine.DiscountAmount` stores the original line discount plus that line's exact invoice-discount allocation.
- `SalesLine.DiscountPercentage` and `SalesLine.ManualDiscountAmount` preserve the original line-discount inputs.
- `SalesLine.LineTotal` stores the final VAT-inclusive value after both discount levels.
- Existing taxable, VAT and inclusive snapshot columns store the immutable tax result.

The exact invoice-discount allocation for a line remains derivable as:

```text
persisted SalesLine.DiscountAmount
minus original line discount derived from DiscountPercentage or ManualDiscountAmount
```

`SalesHeader.InvoiceDiscountAmount` is intentionally a non-mapped checkout request value. It is used by the authoritative repository calculation before the persisted totals and line allocations are saved.

## Preserved behavior

- Stock Item barcode, SKU and Product Seek paths
- GRN batch-barcode priority
- Batch and expiry validation
- Available-stock quantity limits
- Batch stock deduction
- SALE inventory transaction creation
- Stock Item batch cost and profit
- Customer retail and wholesale price selection
- Existing payment and split-tender behavior
- Existing invoice sequence and invoice number generation
- Existing post-commit receipt and cash-drawer invocation
- Gift-voucher and free-issue behavior

Invoice discounts are blocked for carts containing gift-voucher or free-issue lines because their final VAT treatment remains intentionally unresolved.

## Runtime validation gate

Before Phase 7E2 is merged, perform one focused mixed-sale smoke test:

1. Add one Standard VAT Stock Item.
2. Add one Standard VAT Service.
3. Apply a line discount.
4. Apply an invoice discount.
5. Confirm the displayed taxable value, VAT and net total.
6. Complete payment and checkout.
7. Confirm only the Stock Item reduced inventory.
8. Confirm the Service created no inventory transaction.

Receipt and formal Tax Invoice redesign remain Phase 7E3 work.
