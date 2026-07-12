# Phase 7G — VAT Reports and Reconciliation

## Scope

Phase 7G adds a read-only BackOffice VAT reporting page. It does not create a VAT filing, accounting-period lock, payment, amendment, or statutory return record.

## Authoritative values

All report calculations use immutable values saved with completed transactions:

- completed, non-voided sales;
- customer Credit Notes;
- posted GRNs;
- posted supplier Debit Notes.

The report never recalculates historical VAT from the current Tax Master, Item Master, supplier settings, Store Settings VAT rate, or landed cost.

## Date basis

- Sales: `TransactionDate`
- Customer Credit Notes: `ReturnDate`
- GRNs: `InvoiceDate`
- Supplier Debit Notes: `ReturnDate`

Both selected boundary dates are inclusive.

## Operational VAT position

`Net Output VAT = Sales VAT - Customer Return VAT`

`Net Input VAT = GRN product VAT - Supplier Return VAT`

`Operational VAT Position = Net Output VAT - Net Input VAT`

This is an operational control figure, not a filed statutory liability.

## Legacy and freight rules

A document with `LegacyUnknown` or incomplete saved line snapshots is excluded from VAT totals and listed in the separate unknown register. VAT is never invented.

GRN freight remains outside input VAT while its freight tax snapshot is `LegacyUnknown`. The saved freight amount is shown separately in the unknown register.

## Reconciliation

Differences greater than Rs. 0.01 are reported for:

- document-header totals versus saved line snapshots;
- category totals versus saved line categories;
- sales or GRN inclusive values versus document totals;
- cumulative customer returns versus original sale lines;
- cumulative supplier returns versus original GRN lines.

## Database impact

No migration or data update is introduced. The feature is read-only.
