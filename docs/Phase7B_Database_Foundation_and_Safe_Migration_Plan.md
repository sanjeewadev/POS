# Phase 7B — Database Foundation and Safe Migration Plan (Approved)

**Project:** Advanced POS Development  
**Date:** 11 July 2026  
**Status:** Approved on 11 July 2026  
**Based on:** Approved Phase 7A Item, Service and VAT Architecture Specification and the current uploaded source

---

## 1. Purpose

Phase 7B establishes the database foundation required for:

- Stock Items and Services
- authoritative tax categories
- effective-dated VAT rates
- immutable tax snapshots
- VAT-aware purchasing, sales and returns
- auditable selling-price changes from GRN

This phase does **not** rebuild Item Master, PO, GRN, Cashier, receipts or reports. Those pages will be updated only after the database foundation is approved and migrated safely.

---

## 2. Safety principles

The Phase 7B migrations must follow these rules:

1. Existing item, variant, batch and transaction IDs must not change.
2. Every existing item must initially remain a `StockItem`.
3. Existing stock quantities and costs must not be recalculated.
4. Existing historical transactions must not be assigned invented VAT values.
5. Existing legacy tax fields must remain temporarily for compatibility.
6. New foreign keys must initially be nullable where historical data is incomplete.
7. New transaction tax snapshot values must be nullable for legacy rows.
8. Existing historical rows must be marked as tax data unknown where necessary.
9. No current table or column should be dropped in the first foundation migration.
10. Each migration must be tested on a copied SQLite database before normal use.
11. A manual `.posbackup` must be created before applying the first Phase 7B migration.

---

## 3. Confirmed current foundations to preserve

The current system already has useful structures that should remain:

- `ItemParent`
- `ItemVariant`
- `ItemBatch`
- `InventoryTransaction`
- `TaxRate`
- `PoHeader` and `PoLine`
- `GrnHeader` and `GrnLine`
- `SalesHeader` and `SalesLine`
- customer and supplier return entities
- `PriceChangeHistory`
- batch/expiry tracking
- the non-batch `GENERAL` inventory bucket
- direct GRN without a mandatory PO
- existing document sequences
- stock posting and supplier-ledger posting

`HasBatchTracking` must remain a stock-tracking choice. It must never be reused as the service-item flag.

---

# 4. Phase 7B1 — Item type and tax-master foundation

## 4.1 Item type

Add the following column to `ItemParent`:

| Column | Type | Null | Default | Purpose |
|---|---|---:|---|---|
| `ItemType` | string, max 20 | No | `StockItem` | Authoritative item type |

Allowed application values:

- `StockItem`
- `Service`

Add an index on `ItemType`.

### Migration rule

Every existing `ItemParent` row becomes:

```text
ItemType = StockItem
```

No existing item is automatically converted into a Service.

### Existing fields retained

These remain unchanged:

- `HasBatchTracking`
- `HasExpiryTracking`
- `HasBatchExpiry`
- `IsScaleItem`
- `IsSerialized`
- `IsPurchaseLocked`
- `IsSaleLocked`

Later application validation will enforce:

- Service cannot use batch or expiry tracking.
- Service cannot create or use `ItemBatch`.
- Service cannot appear in GRN, stock count or stock adjustment.
- Service can still use the existing Standard variant and variant prices.
- `ItemVariant.CostPrice` can serve as the optional standard service cost.

No new service-cost column is required.

---

## 4.2 New `TaxCategory` table

Create a stable tax-treatment master.

| Column | Type | Null | Notes |
|---|---|---:|---|
| `Id` | int PK | No | Identity |
| `CategoryCode` | string, max 30 | No | Unique stable code |
| `CategoryName` | string, max 100 | No | Display name |
| `TreatmentType` | string, max 30 | No | Application-controlled value |
| `IsRateBased` | bool | No | True only where an effective rate is required |
| `IsActive` | bool | No | Deactivation instead of deletion |
| `DisplayOrder` | int | No | UI order |
| `CreatedAt` | datetime | No | Audit |
| `UpdatedAt` | datetime | No | Audit |
| `DeactivatedAt` | datetime nullable | Yes | Audit |

Seed only these categories:

| Code | Name | Treatment | Rate based |
|---|---|---|---:|
| `STANDARD` | Standard VAT | `StandardRated` | Yes |
| `ZERO` | Zero Rated | `ZeroRated` | No |
| `EXEMPT` | Exempt | `Exempt` | No |
| `OUT_OF_SCOPE` | Out of Scope | `OutOfScope` | No |

Indexes:

- unique `CategoryCode`
- `TreatmentType`
- `IsActive`
- `DisplayOrder`

The UI will show only these four normal choices.

---

## 4.3 Extend the existing `TaxRate` table

Keep the current `TaxRate` table, but make it an effective-dated rate-version table.

Add:

| Column | Type | Null | Purpose |
|---|---|---:|---|
| `TaxCategoryId` | int FK | Yes initially | Rate belongs to a stable category |
| `EffectiveFrom` | datetime | Yes initially | First valid date |
| `EffectiveTo` | datetime nullable | Yes | Last valid date, if closed |
| `ChangeReason` | string, max 250 | Yes | Audit reason |
| `CreatedBy` | string, max 100 | Yes | Audit |
| `UpdatedBy` | string, max 100 | Yes | Audit |

Retain the current fields:

- `TaxCode`
- `TaxName`
- `RatePercent`
- `IsActive`
- `IsSystemDefault`
- `DisplayOrder`
- audit dates

Add:

- FK `TaxRate.TaxCategoryId -> TaxCategory.Id`
- index `TaxCategoryId`
- composite index `(TaxCategoryId, EffectiveFrom)`
- index `(TaxCategoryId, IsActive)`

### Effective-date rule

For a transaction date, the calculation service will select the active rate where:

```text
EffectiveFrom <= transaction date
and
EffectiveTo is null or EffectiveTo >= transaction date
```

Overlapping effective periods will be prevented by the Tax Master service. This does not need a complicated database trigger.

### Existing tax rows

The migration must preserve all existing `TaxRate` rows.

Only an existing row that is unambiguously the standard VAT row may be linked automatically to `STANDARD`.

Legacy rows such as ambiguous tax-free or reduced-rate records must not be silently classified. They may remain temporarily with `TaxCategoryId = null` until reviewed during the Tax Master rebuild.

---

## 4.4 Item-to-tax relationship

Add to `ItemParent`:

| Column | Type | Null | Purpose |
|---|---|---:|---|
| `TaxCategoryId` | int FK | Yes initially | Stable default tax treatment |

Add:

- FK to `TaxCategory`
- index on `TaxCategoryId`

Keep temporarily:

- `ItemParent.TaxCode`
- `ItemParent.IsTaxInclusive`

These are legacy compatibility fields and are not the future source of truth.

### Migration rule

- Existing `VAT-STD` items may be linked to `STANDARD` only when the mapping is unambiguous.
- Ambiguous `TAX-FREE`, reduced-rate or unknown codes remain with `TaxCategoryId = null`.
- No existing item is silently classified as zero-rated, exempt or out-of-scope.
- The rebuilt Item Master will require the user to review unclassified active items.

---

## 4.5 Store VAT registration foundation

Extend `StoreSettings` with:

| Column | Type | Null | Default |
|---|---|---:|---|
| `IsVatRegistered` | bool | No | false |
| `TaxpayerIdentificationNumber` | string, max 30 | No | empty |
| `VatRegistrationNumber` | string, max 30 | No | empty |
| `TaxInvoicePrefix` | string, max 20 | No | `TI` |

Keep `TaxNo` and `GlobalVatRate` temporarily.

`GlobalVatRate` must no longer become a calculation source after the shared tax engine is introduced. It will later be hidden and removed only after all dependencies are eliminated.

---

# 5. Phase 7B2 — PO and GRN tax snapshots

This migration comes after 7B1 is stable.

## 5.1 Shared purchase-line snapshot fields

PO and GRN lines must ultimately contain these values:

| Field | Purpose |
|---|---|
| `TaxCategoryId` | Optional trace to the master |
| `TaxRateId` | Optional trace to the effective rate used |
| `TaxCategoryCodeSnapshot` | Permanent treatment code |
| `TaxCodeSnapshot` | Permanent rate/code identifier |
| `TaxNameSnapshot` | Permanent printed description |
| `TaxRatePercentSnapshot` | Permanent rate |
| `IsTaxInclusiveSnapshot` | Permanent entry mode |
| `TaxableAmount` | Value excluding VAT after discount allocation |
| `VatAmount` | VAT amount |
| `TaxInclusiveAmount` | Value including VAT |
| `TaxSnapshotStatus` | `Complete` or `LegacyUnknown` |

New monetary fields use `decimal(18,2)`.

The effective VAT rate may use `decimal(7,4)` in the model so exact rate snapshots are not restricted to whole or two-decimal percentages.

### Historical migration rule

Existing rows receive:

```text
TaxSnapshotStatus = LegacyUnknown
```

New tax snapshot fields remain null unless the current row already contains enough unambiguous information. The migration must not reconstruct legal VAT values from tax-code text.

---

## 5.2 `PoHeader`

Keep the existing document-level `IsTaxInclusive` field as the supplier-price VAT mode.

Add category totals:

- `StandardRatedAmount`
- `ZeroRatedAmount`
- `ExemptAmount`
- `OutOfScopeAmount`
- `TaxableAmountTotal`
- `TaxSnapshotStatus`

Keep:

- `Subtotal`
- `GlobalBillDiscount`
- `TotalTaxAmount`
- `TotalDiscountAmount`
- `NetPayable`

The rebuilt PO will use one document-level inclusive/exclusive selection. Each line stores that choice as a snapshot.

---

## 5.3 `PoLine`

Preserve:

- `TaxCode`
- `VatRatePercent`
- `IsVatIncluded`
- `TaxAmount`
- `LineTotal`

Add the shared snapshot fields.

The old fields remain until PO logic and reports are fully migrated.

---

## 5.4 `GrnHeader`

Add:

- `IsTaxInclusive`
- `StandardRatedAmount`
- `ZeroRatedAmount`
- `ExemptAmount`
- `OutOfScopeAmount`
- `TaxableAmountTotal`
- `FreightTaxableAmount`
- `FreightVatAmount`
- `FreightTaxCategoryCodeSnapshot`
- `FreightTaxSnapshotStatus`
- `TaxSnapshotStatus`

Keep:

- `Subtotal`
- `GlobalBillDiscount`
- `FreightAmount`
- `TotalDiscountAmount`
- `TotalVatAmount`
- `NetPayable`

The first implementation may keep freight VAT disabled until its accountant-approved treatment is confirmed. The schema allows it without complicating the normal GRN page.

---

## 5.5 `GrnLine`

Preserve:

- `VatRatePercent`
- `IsVatIncluded`
- `VatAmount`
- `LandedCost`
- `LineTotal`
- current and proposed selling-price fields

Add the shared snapshot fields, including a missing permanent tax-code/category snapshot.

Services will be filtered out by application logic after `ItemType` is introduced.

---

# 6. Phase 7B3 — Sales and return tax snapshots

This migration comes after 7B2 is stable.

## 6.1 `SalesHeader`

Add:

| Column | Purpose |
|---|---|
| `DocumentType` | `Receipt` or `TaxInvoice` |
| `TaxInvoiceNo` | Formal tax-invoice number when applicable |
| `IsVatRegisteredSale` | Store VAT-registration snapshot |
| `SupplierTinSnapshot` | Store TIN used on document |
| `SupplierVatNoSnapshot` | Store VAT number used on document |
| `CustomerTinSnapshot` | Purchaser TIN |
| `CustomerVatNoSnapshot` | Purchaser VAT number |
| `CustomerAddressSnapshot` | Purchaser address |
| `TaxableAmountTotal` | Standard-rated value excluding VAT |
| `TotalVatAmount` | VAT total |
| `StandardRatedAmount` | Reporting total |
| `ZeroRatedAmount` | Reporting total |
| `ExemptAmount` | Reporting total |
| `OutOfScopeAmount` | Reporting total |
| `TaxSnapshotStatus` | `Complete` or `LegacyUnknown` |

Keep all current sales totals for compatibility.

`TaxInvoiceNo` will later receive a unique filtered index when formal numbering is implemented.

---

## 6.2 `SalesLine`

Add:

| Column | Purpose |
|---|---|
| `ItemTypeSnapshot` | `StockItem`, `Service`, or special non-item line |
| `TaxCategoryId` | Optional trace |
| `TaxRateId` | Optional trace |
| `TaxCategoryCodeSnapshot` | Permanent treatment |
| `TaxCodeSnapshot` | Permanent code |
| `TaxNameSnapshot` | Permanent display name |
| `TaxRatePercentSnapshot` | Permanent rate |
| `IsTaxInclusiveSnapshot` | Permanent pricing mode |
| `TaxableAmount` | Excluding VAT |
| `VatAmount` | VAT |
| `TaxInclusiveAmount` | Including VAT |
| `TaxSnapshotStatus` | `Complete` or `LegacyUnknown` |

`ItemBatchId` is already nullable in the model. Its navigation property and configuration must be made truly optional.

Later Cashier logic will apply:

- Stock Item: batch required and stock deducted.
- Service: batch null and no inventory transaction.
- Gift voucher or other special lines: controlled by their approved tax treatment.

No sales repository behavior changes are part of the database-only migration.

---

## 6.3 `CustomerReturnHeader`

Add:

- `OriginalSalesHeaderId` nullable FK
- `DocumentType`
- `CreditNoteNo`
- `TaxableAmountTotal`
- `TotalVatAmount`
- `StandardRatedAmount`
- `ZeroRatedAmount`
- `ExemptAmount`
- `OutOfScopeAmount`
- `TaxSnapshotStatus`

Existing `ReturnNo` remains.

---

## 6.4 `CustomerReturnLine`

Add:

- `SalesLineId` nullable FK
- `ItemBatchId` nullable FK
- `ItemTypeSnapshot`
- all shared tax snapshot fields
- `OriginalTaxableAmount`
- `OriginalVatAmount`
- `OriginalTaxInclusiveAmount`

Future returns must reverse the original sales-line snapshots rather than use the current tax rate.

For Services:

- `ItemBatchId` remains null
- no stock movement is created
- the financial credit note is still recorded

---

## 6.5 Supplier returns

Extend `SupplierReturnHeader` with:

- `TaxableAmountTotal`
- `TotalVatAmount`
- category totals
- `TaxSnapshotStatus`

Extend `SupplierReturnLine` with the shared tax snapshot fields and the source GRN tax values being reversed.

Supplier-return lines remain batch-based because only Stock Items are allowed in inventory GRN.

---

# 7. Phase 7B4 — Price-change audit extension

The current `PriceChangeHistory` table is already suitable and must be extended rather than replaced.

It already stores:

- old and new retail price
- old and new wholesale price
- minimum and maximum prices
- user
- date/time
- reason
- source
- item/variant/batch snapshots

Add:

| Column | Type | Purpose |
|---|---|---|
| `SourceDocumentType` | string, max 30 | Example: `GRN` |
| `SourceDocumentId` | int nullable | Source header ID |
| `SourceDocumentLineId` | int nullable | Source line ID |
| `SourceDocumentNo` | string, max 50 | Human-readable source |

No new price-history table is required.

The GRN rebuild will later:

- default `UpdateSellingPrices` to false;
- show current and proposed prices;
- apply only explicitly selected changes;
- save one `PriceChangeHistory` row for each changed price record.

---

# 8. Compatibility fields retained temporarily

The following must not be removed in Phase 7B:

- `ItemParent.TaxCode`
- `ItemParent.IsTaxInclusive`
- `StoreSettings.GlobalVatRate`
- current PO tax fields
- current GRN tax fields
- current sales totals
- current return totals

They will be removed only after:

1. the shared VAT engine is active;
2. all repositories use the new fields;
3. receipts and reports use saved snapshots;
4. migration tests prove historical documents remain readable.

---

# 9. Migration sequence

Create separate migrations:

## 7B1

```text
AddItemTypeAndTaxMasterFoundation
```

Includes:

- `ItemType`
- `TaxCategory`
- effective-date fields on `TaxRate`
- item-to-tax relationship
- Store VAT-registration fields
- seed four tax categories
- safe backfill of all items to `StockItem`

## 7B2

```text
AddPurchaseAndGrnTaxSnapshots
```

Includes:

- PO header/line snapshot columns
- GRN header/line snapshot columns
- `LegacyUnknown` status for existing rows

## 7B3

```text
AddSalesAndReturnTaxSnapshots
```

Includes:

- sales header/line snapshot columns
- customer-return source links and snapshots
- supplier-return snapshots
- optional sales batch relationship correction
- `LegacyUnknown` status for existing rows

## 7B4

```text
ExtendPriceChangeHistorySourceAudit
```

Includes:

- source document audit columns

Each migration must be build-tested and runtime-tested separately.

---

# 10. SQLite migration requirements

Because this project uses SQLite:

- prefer additive `AddColumn`, `CreateTable`, `CreateIndex` and `AddForeignKey` operations;
- avoid unsupported direct `AlterColumn` operations;
- avoid dropping or renaming current columns in Phase 7B;
- when a table rebuild is unavoidable, use explicit SQLite-compatible SQL and preserve every column, index, ID and relationship;
- test migration application from the current real schema;
- test a second startup to confirm migrations are not reapplied;
- test the manual backup and restore path before migration application.

The 7B1 migration should be designed so it does not require a table rebuild.

---

# 11. Required automated tests before UI development

At minimum:

1. Existing items migrate to `StockItem`.
2. Item, variant, batch and transaction IDs remain unchanged.
3. Existing stock totals remain unchanged.
4. Four tax categories are seeded once.
5. Effective-rate selection returns the correct rate for a date.
6. Overlapping rate periods are rejected by the service.
7. Ambiguous legacy tax codes are not silently classified.
8. Existing transaction rows receive `LegacyUnknown`.
9. New transaction rows can save complete tax snapshots.
10. A Service can exist without any batch.
11. A Stock Item continues using the existing stock model.
12. Backup and restore work after all 7B migrations.
13. Historical transaction values do not change when a new tax rate is added.

---

# 12. Approval decision

This plan recommends:

- one authoritative `ItemType` on `ItemParent`;
- one stable `TaxCategory` table;
- effective-dated rows in the existing `TaxRate` table;
- nullable tax master links plus immutable transaction snapshots;
- no invented VAT for historical data;
- separate small migrations;
- extension of the current `PriceChangeHistory` rather than replacement;
- no large UI or repository rewrite inside Phase 7B.

After approval, implementation should begin only with **7B1 — Item Type and Tax Master Foundation**.
