# Advanced POS — Production Backup and Recovery

## Backup policy

Create a verified SQL Server backup after store closing and before every application upgrade. Copy at least one current backup outside the server computer.

Recommended minimum retention:

- seven daily backups;
- four weekly backups;
- one month-end backup for each retained accounting period.

## Create a backup

On the server computer, open:

`Start Menu → Advanced POS → Backup POS Database`

Choose a destination that is accessible to the technician account. The utility:

1. creates the backup in SQL Server's approved backup directory;
2. runs `RESTORE VERIFYONLY WITH CHECKSUM`;
3. copies the verified `.bak` to the selected destination;
4. creates a SHA-256 file next to the backup.

A copied file is not considered a proven recovery point until a restore test has succeeded.

## Integrity check

Run:

`Start Menu → Advanced POS → Check POS Database`

This executes `DBCC CHECKDB` and prints table, row, and migration information.

## Restore

1. Close BackOffice on the server.
2. Close every Cashier terminal.
3. Confirm that no sale or shift operation is in progress.
4. Open `Restore POS Database` as Administrator.
5. Select the verified `.bak` file.
6. Type `RESTORE` exactly when prompted.
7. Enter the SQL application password.
8. Wait for restore, migration compatibility, restricted-login repair, integrity checking, and verification backup to finish.
9. Open BackOffice and inspect users, items, stock, sales, shifts, ledgers, licences, and settings.
10. Open one Cashier and complete a controlled test sale.

## Replacement server recovery

1. Prepare a stable replacement Windows computer.
2. Use the same server LAN address where practical.
3. Run the server installer.
4. Choose **Restore an existing SQL Server backup**.
5. Select the latest verified `.bak`.
6. Recreate the restricted SQL login using the recorded password.
7. Reconfigure terminal profiles only when the server address changed.
8. Generate new machine-bound licences when server or terminal machine codes changed.

## Data safety

Never delete the old server, original SQLite database, or last verified backup until the replacement server has passed the customer acceptance checklist.
