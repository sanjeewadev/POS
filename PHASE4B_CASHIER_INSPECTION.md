# Phase 4B Cashier Source Inspection

## Confirmed findings

- Cashier startup currently creates a shift silently with opening cash `0.00`
  after successful login.
- `ShiftSession` already contains the required audit values:
  terminal number, cashier name, start time, status, and opening cash.
- An `OpenCloseShiftViewModel` exists, but no corresponding Open Shift view
  was included and it was not connected to startup.
- `OpenCloseShiftViewModel` still defaults its terminal to `01`.
- `SalesViewModel` also defaulted to terminal `01` and started an unobserved
  shift lookup from its constructor.
- Startup, inactivity logoff, Sales logoff, and Shift Menu logoff used
  different Login → Sales routes.
- A different Manager/Admin user could take over another cashier's open
  shift through the old terminal-override path.
- The current inactivity action says "locked" but actually logs the user out.
- The existing lock screen code is incomplete and still contains old PIN-era
  remnants.

## Phase 4B-1 patch scope

This patch:

- removes silent zero-cash shift creation from startup;
- adds an explicit Open Shift window;
- records the entered opening cash;
- prevents a second open shift for the same terminal;
- requires the cashier who owns an existing shift to log in;
- removes Manager/Admin takeover of another cashier's shift;
- passes the real terminal and shift into SalesViewModel;
- centralizes Login → Open Shift → Sales and Sales → Login transitions;
- keeps the shift open during logoff;
- does not add PIN or barcode unlock;
- does not yet implement the final configurable auto-lock system.

No database migration is required for Phase 4B-1 because the existing
ShiftSessions table already contains OpeningCash, TerminalNo, CashierName,
StartTime, and Status.
