# Advanced POS — Terminal Replacement

A terminal number identifies a Cashier workstation. It must not be active on two physical computers.

## Replace a failed Cashier computer

1. Confirm that the old Cashier application is closed and will not return to service.
2. In BackOffice Terminal Management, record the old terminal number and release or reassign the old machine registration using the approved support procedure.
3. Install Windows and connect the replacement computer to the trusted store LAN.
4. Run the Cashier installer using the existing terminal number and terminal name.
5. Record the new machine code.
6. Generate a new terminal licence for the new machine code.
7. Import the licence.
8. Configure the printer, drawer, scanner, scale, display, and EFTPOS settings.
9. Open and close a controlled test shift.

## Move Cashier to another terminal number

Do not overwrite an existing terminal assignment. Release the old assignment in BackOffice first, then rerun **Configure Advanced POS Cashier** with the new unique number.

## Server address change

Rerun the Cashier configuration wizard on every terminal using the new server IP. The wizard rewrites the encrypted profile for the current Windows account and verifies the connection.

## Release before replacement

In BackOffice, open Terminal Management, select the terminal, and choose **RELEASE MACHINE**. The operation preserves historical sales but is blocked by an Open/Closing shift or an Active/Held cart. Configure the replacement computer only after release and issue a new machine-bound terminal licence.
