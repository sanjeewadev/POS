# Phase 3 Patch — Remove Legacy Universal Login Bypass

This patch removes the hard-coded `sa / sa123` authentication path and all
special privilege logic based on user ID `0`.

It does not change the database schema and does not implement the future
offline emergency-support challenge/response system.

## Files replaced

- `POS.Core/Services/AuthService.cs`
- `POS.BackOffice.UI/ViewModels/UserManagementViewModel.cs`

## Apply

1. Close BackOffice, Cashier, and Visual Studio.
2. Extract this ZIP into the POS repository root with overwrite enabled.
3. Build Debug and Release.
4. Confirm the real Store Administrator can log in.
5. Confirm `sa / sa123` is rejected in both BackOffice and Cashier.
6. Confirm user management still opens for the Store Administrator.
7. Commit only after all checks succeed.

## Verification search

```powershell
Get-ChildItem -Recurse -File -Include *.cs,*.xaml |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.vs)\\' } |
    Select-String -Pattern 'sa123|Admin123|SKELETON KEY|IsSuperAdmin|backdoor' `
        -CaseSensitive:$false
```

Expected: no results from active source files.
