# Advanced POS — Production Server Installation

## Purpose

The server computer hosts SQL Server Express, the production POS database, BackOffice, database recovery tools, and optionally Cashier Terminal 01.

## Before installation

- Use a stable Windows 11 64-bit computer with an SSD and at least 8 GB RAM.
- Prefer wired Ethernet and configure a router DHCP reservation for the server.
- Connect the computer only to the trusted private store LAN.
- Close all existing POS applications.
- For migration, make two independent copies of the existing `pos_local.db` file.
- For recovery, verify the SHA-256 of the selected `.bak` backup.
- Keep the SQL application password in the technician record; do not give it to normal Cashier users.

## Installation

1. Sign in using the Windows account that will normally run BackOffice.
2. Run `Advanced_POS_Server_Setup_1.0.2.exe` as Administrator.
3. Choose whether Cashier should also be installed on the server computer.
4. The production wizard opens automatically.
5. Select one mode:
   - **New store** — creates a clean `POS_Production` database.
   - **Migrate SQLite** — copies all supported data from a verified standalone database.
   - **Restore backup** — restores a verified SQL Server `.bak` file.
6. Confirm the server LAN IP, fixed SQL port, database name, restricted login, and strong password.
7. Confirm that the active network is the trusted private store LAN.
8. When Cashier is enabled on the server, assign a unique terminal number and name.
9. Import a signed licence when available, or finish setup and generate the licence from the displayed machine code.

## Installer results

The installer creates:

- BackOffice under `C:\Program Files\Advanced POS\Server\BackOffice`;
- optional Cashier under `C:\Program Files\Advanced POS\Server\Cashier`;
- an encrypted per-user database profile under `%LOCALAPPDATA%\POS`;
- non-secret deployment metadata under `%ProgramData%\Advanced POS`;
- setup and terminal reports under `%ProgramData%\Advanced POS\Reports`;
- SQL network rollback information under `%ProgramData%\Advanced POS\Recovery`;
- Start Menu tools for backup, restore, integrity checking, and server status.

The installer does not put the SQL password into reports, shortcuts, command-line arguments, or plain-text configuration files.

## Required acceptance checks

- SQL Server service is `Running` and starts automatically.
- SQL TCP port is reachable from every Cashier computer.
- The firewall rule is Private-profile, TCP-only, and LocalSubnet-only.
- BackOffice opens using the central SQL database.
- Migrated row counts match the setup report.
- `Check POS Database` passes.
- `Backup POS Database` creates a `.bak` and SHA-256 file.
- The backup is restored successfully during the controlled acceptance test.

## Daily operation

The POS Server must be running whenever a Cashier or BackOffice computer needs the shared database. It may be shut down after all shifts are closed, applications are closed, and the daily backup is completed.

## Uninstall safety

Uninstalling the application files does not delete the production SQL database, SQL backups, encrypted profile, licences, or deployment reports. Customer data removal requires a separate deliberate administrative operation.

## Phase 11D.1 role and prerequisite controls

Choose **Server and BackOffice** when the server computer will not process sales. That role does not reserve Terminal 01 or any other Cashier terminal.

Choose **Server, BackOffice, and Cashier** only when the same physical computer will also operate as a checkout. The installer then binds the selected terminal to that computer.

The customer-ready Server setup includes the approved Microsoft-signed SQL Server 2022 Express Core prerequisite. Existing `SQLEXPRESS` installations are detected and retained. A Server setup built without the prerequisite is diagnostic only and must not be given to a customer whose computer may not already contain SQL Server.

## Version 1.0.2 connection-resilience rules

The Server deployment wizard saves the local BackOffice and same-computer Cashier database endpoint as `localhost,1433`. Do not replace this with the Wi-Fi or Ethernet IP address on a one-computer installation.

For remote Cashiers, record the Server PC computer name displayed by the wizard. Configure a DHCP reservation for the Server PC as a backup, keep the computer name stable, and disable sleep during store hours.

The SQL Server service is configured for Automatic startup and three controlled restart actions. After setup, run **POS Server Status** from the Advanced POS Start-menu folder and confirm:

- SQL Server status is Running;
- startup type is Automatic;
- TCP port 1433 succeeds;
- the displayed Server computer name is correct;
- the current LAN IPv4 address is expected.
