# Advanced POS — Production Upgrade Guide

## Release compatibility

BackOffice, Cashier, database setup, and deployment wizard must come from the same release. The applications verify the latest SQL Server migration before normal operation and refuse an incompatible central database.

## Upgrade order

1. Record the current application version and source/release hash.
2. Close all Cashier shifts.
3. Close all Cashier and BackOffice applications.
4. Create and verify a production SQL backup.
5. Upgrade the server installer first.
6. Run the matching server setup/upgrade utility once.
7. Run the integrity and server-status tools.
8. Upgrade every Cashier to the same version.
9. Verify terminal licences and hardware settings.
10. Complete a controlled test sale, return, report, backup, and restart.

## Rules

- Only the server setup/update process may apply SQL Server migrations.
- Cashier installers must never change the production schema independently.
- Do not mix old and new Cashier versions after a schema upgrade.
- Do not delete the pre-upgrade backup until the upgraded store passes acceptance.
- Uninstall does not remove customer database or backups.
