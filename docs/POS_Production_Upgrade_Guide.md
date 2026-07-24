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
- **Upgrade application files and open Server upgrade or repair after setup** — use only when the server connection, server role, database configuration, or Cashier terminal assignment must be changed.

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

## Upgrade to Version 1.0.2

Version 1.0.2 adds store-connection recovery without changing the database schema.

1. Close all Advanced POS applications.
2. Create and verify a production database backup.
3. Run `Advanced_POS_Server_Setup_1.0.2.exe` on the Server PC first.
4. Choose the option that keeps the current configuration unless the local Server profile must be repaired.
5. Open BackOffice and verify store data.
6. Run `Advanced_POS_Cashier_Setup_1.0.2.exe` on each remote Cashier.
7. Keep the current configuration or open repair when the saved server address is obsolete.
8. Prefer the Server PC computer name; otherwise use its reserved IP.
9. Restart Cashier after connection repair.
10. Complete a sale, verify stock, restart the Server, and restart one Cashier.

Do not uninstall Version 1.0.1 before this normal in-place upgrade. The permanent Server and Cashier AppIds remain unchanged.

## Upgrade to Version 1.0.3

Version 1.0.3 hardens production setup, upgrade, repair, and the deployment-wizard layout without changing the database schema.

1. Close all Advanced POS applications and create a verified backup.
2. Run `Advanced_POS_Server_Setup_1.0.3.exe` first.
3. For a normal file-only update, keep the current configuration.
4. When configuration opens, the wizard inspects the SQL installation and automatically selects **Upgrade or repair existing store** when a valid database exists.
5. Confirm the database name and restricted login, enter the SQL application password when it was not loaded from the encrypted profile, and start the upgrade/repair.
6. Setup verifies database identity and integrity, creates a pre-upgrade backup, applies pending migrations, repairs the restricted login/profile, verifies the application connection, and creates a post-upgrade backup.
7. Run `Advanced_POS_Cashier_Setup_1.0.3.exe` on each Cashier after the Server passes acceptance.

Do not select New Store for an existing customer. New Store refuses existing database or login resources and never replaces them.
## Upgrade to Version 1.0.5

Version 1.0.5 repairs SQL Server operational-text collations, removes unsafe UOM fallback queries, hardens Item Master matrix/save recovery, aligns release versioning from one central source, and clarifies the provider-aware backup page.

1. Close BackOffice and every Cashier application.
2. Run `Advanced_POS_Server_Setup_1.0.5.exe` on the BackOffice/SQL Server computer first.
3. Keep the current configuration for a normal in-place upgrade.
4. The Server upgrade creates a verified pre-upgrade backup and applies `20260724043000_RepairOperationalTextCollations`.
5. Confirm GRN, Purchase Order, Price Management, Item Master save/retry, and SQL Server backup tools.
6. Run `Advanced_POS_Cashier_Setup_1.0.5.exe` on every Cashier computer.
7. Keep the current configuration and complete the customer acceptance checklist.

Do not uninstall the previous version and do not run the Cashier installer before the Server upgrade passes.
