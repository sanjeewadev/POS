# Phase 3.1 — First-Run Administrator Provisioning

This patch adds first-run administrator creation without changing the database schema.

Files included:
- Modified `POS.Core/Repositories/UserRepository.cs`
- New `POS.Core/Utilities/PasswordPolicy.cs`
- New `POS.BackOffice.UI/ViewModels/FirstRunAdminViewModel.cs`
- New `POS.BackOffice.UI/Views/FirstRunAdminWindow.xaml`
- New `POS.BackOffice.UI/Views/FirstRunAdminWindow.xaml.cs`
- Modified `POS.BackOffice.UI/App.xaml.cs`

Apply:
1. Close Visual Studio debugging, BackOffice, and Cashier.
2. Extract this ZIP into the repository root:
   `C:\Users\Sanjeewa\Dev\MyProjects\POS`
3. Allow Windows to replace the two existing files.
4. Open the solution and build Debug and Release.
5. Run BackOffice.
6. Because the current migration-managed database has no users, the initial setup window should appear.
7. Create a real administrator, then sign in using that account.

Important:
- The old `sa / sa123` bypass still exists temporarily.
- Do not release or distribute this build.
- After the new administrator login is verified, the next change removes the old universal bypass.
