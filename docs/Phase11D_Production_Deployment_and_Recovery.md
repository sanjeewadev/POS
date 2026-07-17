# Phase 11D — Production Deployment and Recovery

## Scope

Phase 11D converts the proven SQL Server Network Edition into two customer-facing installation packages without changing sales, VAT, stock, return, voucher, credit, or reporting rules.

## Deliverables

- `Advanced_POS_Server_Setup_1.0.0.exe`
- `Advanced_POS_Cashier_Setup_1.0.0.exe`
- guided Server and Cashier deployment wizard;
- production new-store, SQLite-migration, and SQL-backup restore paths;
- restricted SQL application login and encrypted per-user profile;
- machine-safe terminal binding and signed licence import;
- Private/LocalSubnet-only SQL firewall configuration;
- database backup, restore, integrity, and status tools;
- schema compatibility check pinned to the Version 1.0 SQL Server migration;
- SHA-256 and release-manifest generation;
- production installation, recovery, upgrade, terminal-replacement, and acceptance documents.

## Installation roles

### Server setup

Installs SQL Server support, BackOffice, production database tools, recovery tools, and optionally Cashier on the server computer.

### Cashier setup

Installs only the Cashier application and terminal configuration components. It cannot create or migrate the production schema.

## Safety rules

- Production provisioning refuses an existing database or SQL login.
- A failed fresh provisioning run cleans up only the database/login created by that run.
- SQLite migration reads the selected source and does not replace the source database.
- Production restore requires explicit destructive confirmation.
- An existing production database receives a verified safety backup before restore.
- External `.bak` files are staged in SQL Server's approved backup directory.
- Installer uninstallation does not delete databases, backups, profiles, licences, or reports.
- SQL credentials are passed through a process environment variable, never command-line arguments or reports.
- Every Cashier machine must use a unique terminal number and machine registration.

## Build gate

Run `tools\deployment\Build-POS-Production-Installers.ps1` only from a committed, clean source tree. The builder parses PowerShell and XAML, builds Debug and Release, runs core, SQLite Cashier, and SQL Server Cashier audits, publishes self-contained `win-x64` applications, compiles both Inno Setup packages, and emits hashes and a release manifest.

## Exclusions

Phase 11D does not add new business features, cloud services, offline Cashier synchronization, automatic cloud backup, or unrelated UI redesign.
