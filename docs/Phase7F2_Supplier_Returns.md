# Phase 7F2 — Supplier Returns

## Scope

- Posted-GRN-linked Stock Item returns only.
- Exact original batch deduction.
- Current-stock and remaining-GRN-quantity validation.
- Supplier credit reverses the original saved GRN product payable snapshot.
- Landed cost remains the inventory valuation used by the stock transaction.
- Original VAT-inclusive or VAT-exclusive tax snapshots are reversed without using current Tax Master values.
- Exact final residual allocation across multiple partial returns.
- Supplier ledger debit-note posting and authenticated user audit.
- Formal supplier debit-note preview.
- No Services, blind returns, restocking fee, freight credit, selling-price change, or database migration.

## Historical data

Legacy GRNs use their saved line total for the supplier financial credit. VAT values remain null and the supplier return remains `LegacyUnknown`; VAT is never invented.
