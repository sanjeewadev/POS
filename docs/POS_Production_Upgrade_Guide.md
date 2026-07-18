# Advanced POS — Production Upgrade and Repair Guide

## Release compatibility

BackOffice, Cashier, database setup, and deployment wizard must come from the same release. The applications verify the latest SQL Server migration before normal operation and refuse an incompatible central database.

## Upgrade order

1. Record the current application version and release SHA-256 values.
2. Close all Cashier shifts.
3. Close every Cashier and BackOffice application.
4. Create and verify a production SQL backup.
5. Run the newer Server installer first.
6. Select **Upgrade or repair application files and keep the current configuration**.
7. Run the integrity and server-status tools.
8. Run the matching Cashier installer on every terminal.
9. Select the same keep-configuration option.
10. Verify terminal licences and hardware settings.
11. Complete a controlled test sale, return, report, backup, and restart.

## Maintenance choices

When an existing installation is detected, setup offers:

- **Upgrade or repair application files and keep the current configuration** — the normal update and repair path. It replaces application files and shortcuts without reopening setup configuration.
- **Upgrade application files and open configuration after setup** — use only when the server connection, server role, database configuration, or Cashier terminal assignment must be changed.

Running the same version again is a repair. Running a later version is an in-place upgrade.

## Preserved information

The maintenance path preserves:

- the SQL Server production database;
- store settings, users, items, stock, customers, suppliers, sales, and reports;
- terminal assignments and machine registrations;
- store and terminal licences;
- encrypted per-user database profiles;
- printer, cash-drawer, scanner, scale, pole-display, and EFTPOS settings;
- production backup files.

## Rules

- Only the server setup/update process may apply SQL Server migrations.
- Cashier installers must never change the production schema independently.
- Do not mix old and new Cashier versions after a schema upgrade.
- Do not choose reconfiguration during a normal application-only update.
- Do not delete the pre-upgrade backup until the upgraded store passes acceptance.
- Uninstall does not remove the customer database or production backups.
