# Phase 4C — Exit, Login UI, and Local Error Logging

## Included

- One controlled BackOffice exit path for File → Exit and the window X.
- Authenticated BackOffice session cleared during application exit.
- No separate Logout button.
- Redesigned BackOffice login window.
- Redesigned first-run Administrator setup window.
- Shared offline local logging for BackOffice and Cashier.
- Global WPF UI exception logging.
- Application-domain exception logging.
- Unobserved background-task exception logging.
- Friendly startup and login error messages.
- No database migration.

## Log location

```text
%LocalAppData%\POS\Logs
```

The logger creates one daily file per application:

```text
BackOffice_YYYY-MM-DD.log
Cashier_YYYY-MM-DD.log
```

No logs are uploaded anywhere.

## Apply

1. Confirm the repository is committed and clean.
2. Create one manual POS backup.
3. Close BackOffice, Cashier, and Visual Studio.
4. Extract this ZIP directly into:

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

## Initial tests

### BackOffice Login UI

1. Start BackOffice.
2. Confirm the redesigned login window appears.
3. Test one incorrect password.
4. Confirm the normal friendly login error appears.
5. Sign in successfully.

### BackOffice Exit

1. Select File → Exit.
2. Choose No and confirm BackOffice stays open.
3. Select File → Exit again and choose Yes.
4. Confirm BackOffice closes completely.
5. Reopen and sign in.
6. Click the window X.
7. Choose No and confirm it stays open.
8. Click X again and choose Yes.
9. Confirm BackOffice closes completely.

### First-run UI

The first-run Administrator window must only be tested with a separate empty
development database. Do not delete users from the normal development
database.

### Logs

Open:

```text
%LocalAppData%\POS\Logs
```

A log file may not exist until an actual technical exception is recorded.
Normal invalid-password messages are audited in the database and do not
create technical error logs.
