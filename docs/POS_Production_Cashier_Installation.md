# Advanced POS — Production Cashier Installation

## Before installation

Collect these values from the server installation report:

- server LAN IP or reserved host name;
- SQL TCP port;
- production database name;
- restricted SQL application login;
- SQL application password;
- unique terminal number and terminal name.

Each physical Cashier computer must have a different terminal number.

## Installation

1. Sign in using the Windows account that will normally run Cashier.
2. Connect the computer to the trusted store LAN.
3. Run `Advanced_POS_Cashier_Setup_1.0.3.exe` as Administrator.
4. The Cashier production wizard opens automatically.
5. Enter the server connection details and the unique terminal identity.
6. Enter the SQL password in the masked password fields.
7. Select a signed terminal licence when it is already available.
8. Start setup.

The wizard verifies the server endpoint, writes the encrypted per-user profile, checks the restricted SQL login, binds the machine name and machine code to the terminal, and creates a report.

## Licence workflow

When no terminal licence is selected during setup, record the machine code shown at completion. Generate a terminal licence using the exact:

- Store ID;
- terminal number;
- machine code;
- required expiry date.

Start **Activate Advanced POS Cashier** from the Start Menu. The activation window displays the current machine code, can copy or save a complete offline licence request, and imports the signed `.poslic` file directly on the target Cashier computer.

BackOffice Terminal Management can copy the registered machine information for a remote terminal, but the final terminal licence is imported on the physical Cashier computer that will use it.

## Hardware setup

After licensing:

1. Open Cashier.
2. Verify the terminal number shown by the application.
3. Configure the local receipt printer, paper width, cash drawer, scanner suffix, scale, display, and EFTPOS options as required.
4. Print a test receipt and test the drawer.

## Acceptance checks

- login succeeds with an active Cashier user;
- the terminal opens only its own shift;
- item and customer changes from BackOffice are visible;
- a sale appears in BackOffice Sales Explorer after Load/Refresh;
- receipt and drawer actions work;
- network interruption blocks incomplete posting;
- restart and reconnection work;
- the terminal licence matches the machine code and terminal number.

## Existing standalone data

The Cashier installer does not delete an existing `%LOCALAPPDATA%\POS\pos_local.db`. The encrypted network profile causes the installed production Cashier shortcut to use SQL Server. Keep the old standalone database only as a controlled migration backup, not as a second live production database.

## Re-running configuration

The **Configure Advanced POS Cashier** Start-menu entry can be run again after an interrupted or failed configuration. Repeating the same terminal assignment on the same computer is safe and verifies the existing configuration.

When the selected terminal belongs to another computer, do not choose a different number merely to bypass the message. Release the old machine through BackOffice **Terminal Management → RELEASE MACHINE**, then retry configuration. The Cashier installer never installs SQL Server.

## Missing or expired licence

Starting Cashier with a missing, expired, or invalid terminal licence opens **Advanced POS — Cashier Activation Required** instead of closing. Sales remain locked, but the operator or technician can copy the machine code, save a licence request, import a renewed terminal licence, and continue to login without restarting the application.

A store licence problem must be corrected from BackOffice on the server computer. The Cashier activation window accepts terminal licences only.

## Upgrading or repairing an installed Cashier

Run the matching newer Cashier installer over the existing installation. Select **Upgrade or repair application files and keep the current configuration**. The encrypted database profile, terminal assignment, machine registration, licence, and local hardware settings are preserved. Select the reconfiguration option only when those settings genuinely need to change.

## Version 1.0.2 server-address rule

For a remote Cashier, enter the Server PC computer name when the store network resolves Windows computer names. Example: `POS-SERVER`. This normally survives router DHCP address changes.

Use a numeric IP address only when computer-name resolution is unavailable. In that case, the router must reserve that address for the Server PC.

When repairing an existing Cashier, open **Repair or Configure Advanced POS Cashier**. The current encrypted database name, login, password, port, terminal number, and terminal name are reloaded when available. Change only the server computer name or IP unless another setting genuinely changed.

After a repair, restart Cashier so the new encrypted profile is loaded.
