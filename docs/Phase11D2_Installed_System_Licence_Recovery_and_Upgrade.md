# Phase 11D.2 — Installed-System Licence Recovery and Upgrade

## Purpose

Phase 11D.2 completes the installed Cashier recovery path and makes the Server and Cashier installers safe to run over an existing installation.

## Cashier licence recovery

Cashier no longer closes when its terminal licence is missing, expired, or invalid. Before login it opens **Advanced POS — Cashier Activation Required** and keeps business transactions locked.

The recovery window provides:

- the current Store ID;
- terminal number and name;
- computer name;
- machine code;
- store and terminal licence status;
- terminal expiry date;
- application version;
- Copy Machine Code;
- Copy Licence Request;
- Save Licence Request;
- Import Terminal Licence;
- Check Again;
- Configure Connection;
- Continue to Cashier after successful validation.

The saved request is a normal text file suitable for USB transfer in an offline store.

Cashier accepts only a signed terminal licence. Store licences remain a BackOffice responsibility. A wrong-store, wrong-terminal, wrong-machine, damaged, modified, or incorrectly signed licence is rejected without replacing the existing licence.

Importing the same valid licence again is idempotent. The existing record is reverified rather than duplicated.

## BackOffice remote terminal request

BackOffice Terminal Management can copy a complete licence request for the selected registered Cashier computer. The request uses the machine name and machine code already registered by that physical terminal.

The signed `.poslic` file must still be imported on the target Cashier computer through the recovery window.

## Installed upgrade and repair

The Server and Cashier installers retain their stable App IDs. When an existing installation is detected, setup offers:

1. upgrade or repair application files while preserving the current configuration; or
2. upgrade application files and open configuration after setup.

The default is the first option. It does not rerun database or terminal configuration.

The installers preserve data stored outside the application directory, including:

- the production SQL Server database;
- encrypted connection profiles;
- terminal assignment and machine registration;
- installed licences;
- printer and cash-drawer settings;
- backup files.

Running the same version again acts as a repair of installed application files and shortcuts. Running a later version performs an in-place application upgrade.

## Required store upgrade order

1. Close all Cashier applications and BackOffice.
2. Create and verify a SQL Server backup.
3. Run the matching Server installer first.
4. Select **Upgrade or repair application files and keep the current configuration**.
5. Verify BackOffice, database status, and licence status.
6. Run the matching Cashier installer on every terminal.
7. Select the same keep-configuration option.
8. Use **Activate Advanced POS Cashier** only when licence renewal or recovery is required.
9. Complete a controlled sale and verify it in BackOffice.

## Security and logging

The recovery window does not provide POS login, sales, stock, reports, user management, or licence generation. It only exposes non-secret machine/request information and validates signed terminal licences.

Technical failures are written to the local POS log. Passwords, signing private keys, and complete confidential licence payloads are not displayed or logged.
