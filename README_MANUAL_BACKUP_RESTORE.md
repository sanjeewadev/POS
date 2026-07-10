# Final Manual Backup / Restore Patch

This patch keeps the feature completely offline and manual.

## Changes

- No scheduled, startup, background, cloud, or automatic backups.
- Administrator-only backup and restore actions.
- User chooses the backup destination.
- New backups use one `.posbackup` file.
- Legacy `.posbak` files remain readable.
- SQLite live snapshot is used instead of copying the active database file.
- Every new backup is immediately re-opened and verified.
- SHA-256 checksum validation.
- SQLite `quick_check`.
- Required POS-table validation.
- EF migration compatibility validation.
- One restore-safety database is created before a manual restore.
- Cashier must be closed before restore.
- Successful restore restarts BackOffice.
- Real authenticated username is written to backup history.
- Compact UI matching the preferred Item Master / Category / License page style.
- No database migration is required.

## Apply

1. Confirm the POS repository is clean.
2. Close BackOffice, Cashier, and Visual Studio.
3. Extract this ZIP into:

```text
C:\Users\Sanjeewa\Dev\MyProjects\POS
```

Choose overwrite.

## Build

```powershell
cd C:\Users\Sanjeewa\Dev\MyProjects\POS

dotnet build .\POS.sln -c Debug
dotnet build .\POS.sln -c Release
```

Both must finish with `0 Error(s)`.

## First test: backup creation only

1. Open BackOffice.
2. Sign in as Administrator.
3. Open File → Backup / Restore.
4. Click Create Backup.
5. Save the file to a temporary test folder.
6. Confirm the page reports successful creation and verification.
7. Click Verify and confirm the selected file is valid.

Do not perform the restore test until the backup creation test has passed and
a separate copy of the current development database exists.
