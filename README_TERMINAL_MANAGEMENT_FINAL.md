# Terminal Management Final Patch

This is the third and final settings-page patch.

## Final page responsibilities

Terminal Management is an Administrator-only local registry of terminal
records.

It supports:

- viewing all registered terminal records;
- identifying the current computer;
- registering or refreshing the current computer;
- renaming a selected terminal;
- activating a selected terminal;
- disabling a selected terminal;
- viewing machine, license, expiry, and registration details;
- copying a machine code.

It does not support:

- arbitrary fake terminal creation;
- deleting transaction-linked terminals;
- editing another computer's printer, drawer, or auto-lock settings;
- remote lock, shutdown, or logout;
- importing license files;
- changing terminal number or machine assignment manually;
- location, terminal-type, BackOffice-access, or remarks editing.

## Correctness changes

- Activating or disabling a terminal also updates the matching
  `TerminalSettings.IsActive` record, so Cashier startup obeys the status.
- Renaming a terminal also updates the matching Terminal Settings record.
- Current-machine registration safely upserts by machine code or terminal
  number.
- Audit updates use the authenticated Administrator username.
- Page loading is controlled from the view instead of a constructor
  fire-and-forget task.
- Technical errors are written to the existing local POS log.

## Database

No migration is required.

## Apply

1. Commit and push the completed Store Settings work.
2. Confirm Git is clean.
3. Create one manual backup.
4. Close BackOffice, Cashier, and Visual Studio.
5. Extract this ZIP directly into:

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

After the builds succeed, all three settings pages can be tested together
before the final settings checkpoint commit.
