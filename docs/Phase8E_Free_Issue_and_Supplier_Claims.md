# Phase 8E — Free Issue Rules and Supplier Claims

## Scope

Phase 8E completes the existing manual Free Issue and Supplier Claim architecture without adding an automatic promotion engine, Buy-X-Get-Y processing, promotion stacking, loyalty, or broad campaign analytics.

The completed workflow supports:

- deliberate cashier application of one rule to one free line;
- operator-entered free quantity;
- paid/free line splitting when only part of a cart line is free;
- item, category, subcategory, parent-item, supplier, and all-item targets already supported by the rule model;
- validity dates, invoice limits, daily limits, and claim-value limits;
- shop-funded and supplier-funded Free Issues;
- authenticated Manager or Administrator approval where the saved rule requires it;
- stock deduction for free Stock Items;
- zero-inventory behavior for free Services;
- immutable rule and approval snapshots on completed sale lines;
- zero-value customer returns for free lines;
- stock restoration for returned free Stock Items and no inventory movement for returned free Services;
- supplier-claim adjustment records for returned supplier-funded free lines;
- Supplier Claim statuses: Draft, Submitted, Settled, and Rejected;
- supplier, promotion, quantity, and claim-value summaries;
- filtered Supplier Claim CSV export;
- compact BackOffice pages using the existing shared light-blue row-selection resource.

## Approved VAT treatment

The approved Phase 8E VAT rule is **Option A — zero-value VAT treatment**.

For a Free Issue line:

- customer payable is zero;
- sales revenue is zero;
- taxable amount snapshot is zero;
- VAT amount snapshot is zero;
- tax-inclusive amount snapshot is zero;
- the item tax category, tax rate, and item type are still snapshotted for audit;
- the Receipt and Tax Invoice label the line as `FREE ISSUE`;
- a supplier recovery value remains a separate Supplier Claim amount and does not become sale revenue or output VAT.

Paid lines in the same sale continue to use the normal VAT engine. A Free Issue line no longer makes the sale tax snapshot incomplete or blocks a valid Tax Invoice.

## Database migration

Migration:

```text
20260713120000_AddFreeIssueSupplierClaimCompletion
```

The migration is additive. It does not rewrite an earlier migration and does not recreate the database.

### Added sale-line and claim audit/snapshot fields

The migration adds the following fields to both `SalesLines` and `FreeItemClaimLogs` where applicable:

- `FreeIssueAppliedBy`
- `FreeIssueAppliedAt`
- `FreeApprovedByUserId`
- `FreeApprovedRole`
- `FreeIssueRuleSnapshotJson`
- `FreeIssueSnapshotStatus`

Historical rows receive `LegacyUnknown` snapshot status. No historical rule configuration is invented.

### Claim-return adjustment table

`FreeItemClaimAdjustments` records one auditable supplier-claim reduction for each returned supplier-funded Free Issue line. It contains:

- supplier claim link;
- customer return line link;
- returned quantity;
- claim-value reduction;
- creation user and timestamp;
- remarks.

A unique index on `CustomerReturnLineId` prevents duplicate adjustment posting.

### Uniqueness and status changes

- `FreeItemClaimLogs.SalesLineId` becomes unique so one completed sale line can create only one Supplier Claim.
- `FreeIssueRules.RuleName` becomes case-insensitively unique.
- historical active `Pending` claim statuses are changed to `Draft` in both the claim and linked sale-line status fields.
- historical `Written Off` and `Cancelled` claim rows remain readable as legacy records, but Phase 8E does not expose new transitions into those states.

If duplicate rule names or duplicate claims already exist, the migration fails and rolls back instead of deleting or guessing which row to keep. A database backup is required before applying the migration to the live store database.

## Authoritative checkout behavior

Cashier validation improves usability, but checkout is authoritative. Inside the sale transaction, checkout revalidates:

- rule existence and active state;
- validity dates;
- item/target eligibility;
- aggregate per-invoice quantity and value limits;
- serialized daily quantity and value limits;
- authenticated approval user and role;
- stock batch validity and stock availability;
- supplier and claim values for supplier-funded rules.

The completed rule snapshot is generated from the database rule at checkout, not trusted from the Cashier UI.

## Supplier Claim lifecycle

Allowed transitions are:

```text
Draft     -> Submitted
Draft     -> Rejected
Submitted -> Settled
Submitted -> Rejected
```

`Settled` and `Rejected` are terminal. Direct `Draft -> Settled` and repeated terminal-state changes are rejected.

A returned supplier-funded Free Issue reduces the net claim quantity and net claim value through an adjustment record. The original claim amount and lifecycle history remain immutable. A return does not silently reopen or rewrite a settled claim.

## Automated regression coverage

The custom console regression runner now registers **202 tests**. Phase 8E adds coverage for:

- date and target eligibility;
- invoice quantity and value limits;
- concurrent daily-limit enforcement;
- explicit approval rules;
- authoritative authenticated approval at checkout;
- immutable rule snapshots;
- shop-funded stock deduction;
- supplier-funded claim creation;
- free Service no-inventory behavior;
- all-free zero-payment checkout;
- VAT Option A zero-value tax snapshots;
- Tax Invoice formatting with a Free Issue;
- valid, invalid, and concurrent Supplier Claim transitions;
- zero-refund free-line return and claim adjustment;
- claim creation idempotency;
- CSV escaping and invariant numeric formatting;
- checkout rollback;
- case-insensitive rule-name uniqueness;
- migration from an empty database;
- migration from the Phase 8D baseline;
- rollback when migration preflight exposes duplicate rule names.

## Runtime gate

After Debug, all automated tests, migration smoke checks, and Release pass:

1. Create one shop-funded rule and one supplier-funded rule.
2. Add a cart quantity greater than one and make only part of it free.
3. Verify separate paid and free lines.
4. Authenticate a Manager-controlled rule and an Administrator-controlled rule as applicable.
5. Complete a free Stock Item sale and a free Service sale.
6. Verify exact stock deduction and no Service inventory movement.
7. Preview or print the Receipt and Tax Invoice and confirm `FREE ISSUE` and zero free-line VAT/value.
8. Return a free Stock Item line and verify zero refund, stock restoration, and supplier-claim adjustment.
9. Open Supplier Claims, filter by supplier/promotion/status, and export CSV.
10. Verify valid Submitted/Settled and Rejected transitions and invalid transition blocking.
11. Confirm compact page layout and the shared light-blue row selection.

## Unchanged areas

Phase 8E does not modify:

- POS.LicenseGenerator;
- the completed one-time Gift Voucher lifecycle;
- unrelated purchasing, shift, customer-credit, or licensing workflows;
- Phase 9A or later roadmap work.
