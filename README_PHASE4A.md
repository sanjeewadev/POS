# Phase 4A — BackOffice Startup and Navigation

## Apply

Close BackOffice and Visual Studio.

Extract this ZIP into:

```text
C:\Users\Sanjeewa\Dev\MyProjects\POS
```

Choose overwrite when asked.

This patch changes only:

```text
POS.BackOffice.UI\App.xaml
POS.BackOffice.UI\ViewModels\MainViewModel.cs
POS.BackOffice.UI\Views\Layout\ManagementShellView.xaml
```

## Build

```powershell
cd C:\Users\Sanjeewa\Dev\MyProjects\POS

dotnet build .\POS.sln -c Debug
dotnet build .\POS.sln -c Release
```

Both builds must finish with `0 Error(s)`.

## Test

Run BackOffice:

```powershell
dotnet run `
    --project .\POS.BackOffice.UI\POS.BackOffice.UI.csproj
```

Verify:

1. Exactly one login window appears.
2. After successful login, Dashboard opens immediately.
3. Dashboard menu opens Dashboard again.
4. File → Terminal Settings works.
5. File → Backup / Restore works.
6. File → Exit asks for confirmation and closes BackOffice.
7. CRM → Free Issue Rules opens the existing page.
8. Price Change History, License Management, Terminal Management, and other
   menu items still open normally.
9. The status bar shows the authenticated username/role, current clock, and
   application version instead of hard-coded text.

Do not commit until the build and these checks pass.

## Not included yet

- BackOffice logout/return-to-login.
- Cashier explicit shift opening.
- Cashier inactivity-route repair.
- Global exception handling and structured logs.
