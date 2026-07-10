# Phase 4B-1 Float Prompt Removal Hotfix

This hotfix changes the new shift dialog to match the existing Cashier design:

- no opening-float input during login;
- shift starts explicitly with `0.00`;
- terminal and operator are still confirmed;
- the existing **Float Cash** button is used after Sales opens;
- Float Cash continues to record the float transaction through the existing
  CashMovement workflow.

## Apply

Close BackOffice, Cashier, and Visual Studio.

Extract this ZIP directly into:

```text
C:\Users\Sanjeewa\Dev\MyProjects\POS
```

Choose overwrite.

Then build:

```powershell
cd C:\Users\Sanjeewa\Dev\MyProjects\POS

dotnet build .\POS.sln -c Debug
dotnet build .\POS.sln -c Release
```

Both builds must finish with `0 Error(s)`.

## Test

1. Close the current test shift using the normal Close Shift workflow.
2. Log in again.
3. Confirm the new **START CASHIER SHIFT** window has no amount field.
4. Click **START SHIFT**.
5. Confirm Sales opens.
6. Use the existing **Float Cash** button to add float.
7. Confirm the float amount is shown and recorded.
