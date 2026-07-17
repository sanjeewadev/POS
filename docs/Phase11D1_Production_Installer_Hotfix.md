# Advanced POS Phase 11D.1 — Production Installer Hotfix

## Purpose

Phase 11D.1 corrects the first clean two-computer production rehearsal without changing sales, VAT, inventory, payment, licensing, or database schema rules.

## Corrected deployment roles

- **Server and BackOffice** installs the database platform and BackOffice only. It does not reserve or bind a Cashier terminal.
- **Server, BackOffice, and Cashier** installs the optional Cashier component and binds the selected terminal to the server computer.
- **Cashier** installs no SQL Server components and binds only the selected Cashier computer.

The Inno Setup component selection is passed explicitly to the deployment wizard. Existing Cashier files from an older server installation are not used to infer the selected role and are removed when a server is reinstalled as BackOffice-only.

## Repeated Cashier configuration

Running Cashier configuration again with the same computer and terminal is safe. The wizard verifies the existing assignment, refreshes the encrypted connection profile, and reports that configuration was already complete.

A terminal assigned to another computer is never stolen automatically. The wizard shows a customer-readable message and points the technician to BackOffice Terminal Management.

## Releasing a machine assignment

BackOffice Terminal Management now provides **RELEASE MACHINE**. This clears only the current machine binding and machine-bound licence snapshot. Historical sales and terminal numbers remain unchanged.

Release is blocked when the terminal has:

- an Open or Closing shift;
- an Active cart;
- a Held cart.

A replacement computer must be configured with the released terminal number and must receive a new machine-bound terminal licence.

## SQL Server Express prerequisite

Customer-ready Server installers must bundle the Microsoft-signed SQL Server 2022 Express Core package named `SQLEXPR_x64_ENU.exe`.

The installer:

1. detects an existing `SQLEXPRESS` service and skips unnecessary installation;
2. installs the bundled prerequisite when the service is absent;
3. verifies the installer exit code and service creation;
4. continues to the Advanced POS database wizard only after the prerequisite succeeds.

The Cashier installer never includes or installs SQL Server.

## Error handling

Customer dialogs no longer display raw .NET stack traces from the database setup utility. They display a controlled explanation and the path to a technical log under:

`C:\ProgramData\Advanced POS\Logs\Setup`

SQL application passwords are supplied through the process environment and are not written to setup reports or command lines.
