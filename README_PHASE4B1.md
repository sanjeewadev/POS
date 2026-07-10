# Phase 4B-1 — Explicit Cashier Shift Opening

## Apply

1. Confirm the POS repository is committed and clean.
2. Close BackOffice, Cashier, and Visual Studio.
3. Extract this ZIP into:

```text
C:\Users\Sanjeewa\Dev\MyProjects\POS
```

Choose overwrite.

## Files changed

```text
POS.Cashier.UI\App.xaml.cs
POS.Cashier.UI\ViewModels\LoginViewModel.cs
POS.Cashier.UI\ViewModels\SalesViewModel.cs
POS.Cashier.UI\Views\LoginView.xaml
POS.Cashier.UI\Views\SalesView.xaml.cs
POS.Cashier.UI\Dialogs\ShiftMenuView.xaml.cs
POS.Core\Repositories\TillRepository.cs
```

## New files

```text
POS.Cashier.UI\ViewModels\OpenShiftViewModel.cs
POS.Cashier.UI\Dialogs\OpenShiftView.xaml
POS.Cashier.UI\Dialogs\OpenShiftView.xaml.cs
```

## Build

```powershell
cd C:\Users\Sanjeewa\Dev\MyProjects\POS

dotnet build .\POS.sln -c Debug
dotnet build .\POS.sln -c Release
```

Both must finish with `0 Error(s)`.

## Before testing

The development database may already contain an open test shift from the
previous automatic behavior.

Check it through the current Shift menu/shift-closing workflow. Do not delete
database records manually.

## Test A — existing open shift

1. Start Cashier.
2. The login page should show the cashier name that owns the open shift.
3. A different user must be rejected.
4. The owning cashier should log in and reach Sales.
5. Confirm the terminal number and shift ID are correct.

## Test B — explicit new shift

Use the approved shift-closing workflow to close the current development
shift first.

1. Start or return to Cashier login.
2. Log in.
3. The new **OPEN CASHIER SHIFT** window must appear.
4. Enter an opening cash amount such as `5000.00`.
5. Click **OPEN SHIFT**.
6. Sales should open only after confirmation.
7. Confirm the new shift stores:
   - correct TerminalNo;
   - correct CashierName;
   - current StartTime;
   - Status = Open;
   - OpeningCash = 5000.00.

## Test C — cancel

1. Close the test shift.
2. Log in again.
3. When Open Shift appears, click Cancel.
4. Sales must not open.
5. No new ShiftSessions row should be created.

## Test D — logoff

1. With a valid open shift, use Log Off.
2. Login should reappear.
3. The shift must remain open.
4. Only the cashier who owns that shift may resume it.

## Not included yet

Phase 4B-2 will add:

- Auto-Lock Timeout (Minutes) in Terminal Settings;
- `0` to disable auto-lock;
- one shared Cashier lock service;
- manual Lock Terminal action;
- F12 lock shortcut;
- password-only unlock for the current user;
- removal of the old conflicting inactivity and PIN-era lock code.
