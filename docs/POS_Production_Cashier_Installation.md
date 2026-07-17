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
3. Run `Advanced_POS_Cashier_Setup_1.0.0.exe` as Administrator.
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

Rerun **Configure Advanced POS Cashier** from the Start Menu and select the generated `.poslic` file, or import it through BackOffice License Management.

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
