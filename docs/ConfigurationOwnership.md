# POS Configuration Ownership

This document defines the authoritative source for operational configuration.

## Store-wide configuration

Authoritative source: `StoreSettings`

Includes:

- legal/business name;
- store/trading name;
- address and contact details;
- BRN and VAT registration number;
- receipt header and footer.

Receipts, quotations, Cashier headings, backup metadata, and barcode-label store
identity must read these values from the active Store Settings row.

## Per-terminal configuration

Authoritative source: `TerminalSettings`

Includes:

- terminal number and terminal name;
- machine assignment;
- receipt printer;
- receipt paper width;
- automatic printing and receipt copies;
- cash-drawer options;
- Cashier auto-lock timeout;
- active/disabled state synchronized by Terminal Management.

`01` is only the centralized initial number for a brand-new database with no
terminal configuration. Transaction paths read the persisted terminal row.

## Terminal registry and licensing identity

Authoritative source: `RegisteredTerminal` plus the installed signed licenses.

`RegisteredTerminal` contains administrative registration, machine code,
activation, and license-related identity. `TerminalSettings` contains local
operational settings. They remain separate and are synchronized where terminal
name or active status overlaps.

## Database location

Authoritative source: `DatabasePathProvider`

The production database is:

```text
%LocalAppData%\POS\pos_local.db
```

No application should create or select another hidden database path.

## Document numbering

Authoritative source: `DocumentSequence`

`DocumentSequence.Prefix`, `NextSequenceNumber`, and `PaddingLength` control
transaction numbers. The hidden prefix fields retained in `StoreSettings` are
legacy compatibility fields and must not be read by transaction services.

## VAT

Authoritative source: `TaxRate` records and item tax assignment.

`StoreSettings.TaxNo` is only the business VAT registration number.
`StoreSettings.GlobalVatRate` is a hidden legacy compatibility field and must
not control transaction VAT calculations.

## Printer ownership

Receipt printer: per-terminal `TerminalSettings.ReceiptPrinterName`.

Barcode/label printer: selected on the barcode-print page for that print job.
The store identity printed on labels comes from Store Settings.

## Backup and deployment

The manual `.posbackup` database backup is the configuration export and restore
mechanism. A second configuration-export feature is intentionally not added.

After restoring a database to different hardware, review Terminal Management,
Terminal Settings, and licensing before starting Cashier.
