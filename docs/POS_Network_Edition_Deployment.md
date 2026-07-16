# Advanced POS Network Edition

## Supported modes

- Standalone: BackOffice and Cashier use the local SQLite database on one Windows computer.
- Network: one SQL Server Express database is hosted on the BackOffice/server computer and one or more Cashier computers connect over the private store LAN.

## Network database rules

- Never place `pos_local.db` in a Windows shared folder.
- SQL Server uses a fixed TCP port, normally 1433.
- The Windows Firewall rule is limited to Private network profiles.
- The POS application login has only data-reader and data-writer database roles. It is not a SQL Server administrator and cannot perform backups or schema changes.
- BackOffice and Cashier never run schema migrations automatically in SQL Server mode.
- Schema creation and upgrades are performed by `POS.Database.Setup` under an authorized Windows administrator account.

## Initial server installation

1. Create a verified SQLite backup and close BackOffice and Cashier.
2. Run `Configure-POS-SqlServer-Network.ps1` as Administrator.
3. Publish or install `POS.Database.Setup` on the server PC.
4. Run `Install-POS-Server.ps1` with a strong application password.
5. Verify the generated setup report, database integrity result, and SQL Server backup.
6. Start BackOffice with the encrypted SQL Server profile and perform the runtime checklist.

## Cashier terminal installation

1. Connect the terminal to the same private LAN.
2. Confirm the server name or fixed IPv4 address and TCP port.
3. Install the Cashier application and terminal licence.
4. Run `Configure-POS-Terminal.ps1` for the Windows user who will run Cashier.
5. Open Cashier, verify terminal registration, printer, drawer, scanner, login, and shift workflows.

## Backup and restore

- Network backups are SQL Server `.bak` files created and verified on the server PC.
- Copy verified backups to separate external storage after creation.
- Restore is destructive, terminates active POS connections, and requires explicit confirmation.
- Do not use the standalone SQLite `.posbackup` workflow for a SQL Server database.

## Failure handling

- A Cashier must not complete a sale when the server connection is unavailable.
- After LAN or server recovery, reopen Cashier and verify the cart/checkout state before retrying.
- Use the generated SQL Server network-settings backup to restore the previous registry and firewall state when setup is abandoned.
