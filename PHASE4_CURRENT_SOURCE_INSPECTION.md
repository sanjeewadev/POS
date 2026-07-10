# Phase 4 Current Source Inspection

## Confirmed BackOffice findings

- BackOffice already uses a modal `LoginWindow` in `App.xaml.cs`.
- `MainViewModel` still creates another `LoginViewModel` and sets it as
  `CurrentPage`, so the post-login shell begins in a conflicting second login
  state.
- `NavigateToDashboardCommand` is referenced by the shell but was missing.
- The Free Issue Rule ViewModel and View both exist and the ViewModel is
  registered, but the route, menu command, and DataTemplate were missing.
- File-menu Backup and Exit items had no commands.
- Several `Button` controls were embedded inside `MenuItem` collections.
- The shell status bar had hard-coded user, date/time, and version text.
- A dedicated BackOffice logout flow does not yet exist.

## Confirmed Cashier findings

- Cashier startup now obtains the terminal number from terminal settings.
  The remaining `01` value is only a first-run fallback.
- Cashier startup is asynchronous; the earlier synchronous
  `.GetAwaiter().GetResult()` problem is no longer present.
- When no shift is open, successful login still silently creates a shift with
  zero opening cash.
- `OpenCloseShiftViewModel` exists but is not used by startup and still
  defaults its own terminal number to `01`.
- Inactivity currently performs a separate logoff/login route that does not
  rebuild shift state consistently.
- Manager takeover of a terminal can continue under the original shift
  cashier name.
- Cashier startup/login event handlers need explicit error handling.

## Shared lifecycle findings

- No global WPF UI exception handler was found.
- No unobserved-task exception handler was found.
- No structured application logging service was found.

## Phase 4A patch scope

This first controlled patch changes only BackOffice startup/navigation:

- removes the second embedded login state;
- opens Dashboard after the successful modal login;
- adds the missing Dashboard command;
- connects Free Issue Rules;
- repairs File-menu Backup and Exit;
- replaces embedded menu Buttons with MenuItems;
- binds status-bar user, clock, and application version to real state.

It does not alter the database, licensing, authentication security,
Cashier shifts, or sales logic.
