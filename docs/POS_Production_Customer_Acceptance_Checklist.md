# Advanced POS — Customer Acceptance Checklist

## Installation identity

- [ ] Release version and SHA-256 recorded.
- [ ] Server computer, LAN IP, database name, and SQL port recorded.
- [ ] Every Cashier terminal number and machine name recorded.
- [ ] Store and terminal licences imported and verified.

## Server and network

- [ ] Server starts without errors.
- [ ] SQL Server service starts automatically.
- [ ] Server sleep is disabled during store hours.
- [ ] Router address reservation is configured.
- [ ] Firewall is restricted to Private/LocalSubnet.
- [ ] All Cashiers reach the SQL endpoint.

## Master data and purchasing

- [ ] Store settings verified.
- [ ] Users and roles verified.
- [ ] Suppliers, customers, Stock Items, Service Items, VAT rates, and prices verified.
- [ ] Purchase Order and GRN completed.
- [ ] Stock balance and batch values verified.

## Cashier and concurrency

- [ ] Separate shifts opened on all Cashiers.
- [ ] Cash sale completed.
- [ ] Card sale completed with reference.
- [ ] Split payment completed.
- [ ] Customer-credit sale completed.
- [ ] Receipt and Tax Invoice printed.
- [ ] Last-stock concurrent sale cannot create negative stock.
- [ ] Same Gift Voucher can be redeemed only once.
- [ ] Same customer return cannot be posted twice.
- [ ] Customer credit cannot exceed its limit under concurrency.

## Cash and reports

- [ ] Float In/Out verified.
- [ ] Paid In/Out verified.
- [ ] X Report verified.
- [ ] Z Report and shift close verified.
- [ ] Sales Explorer, VAT, financial, customer, and supplier reports verified.

## Failure and recovery

- [ ] Cashier is blocked safely when LAN/server is unavailable.
- [ ] Applications reconnect after SQL Server restart.
- [ ] Both computers restart and reconnect successfully.
- [ ] Production backup created with SHA-256.
- [ ] Backup restored into a controlled test/recovery database.
- [ ] Restored application data verified.

## Handover

- [ ] Customer received the installation guide.
- [ ] Customer received the backup/recovery guide.
- [ ] Customer knows the server must remain on during trading.
- [ ] Customer knows where backups are stored.
- [ ] Technician retained no private licensing key or customer password in the store package.

## Version 1.0.2 connection and recovery acceptance

- [ ] Server and same-computer Cashier use `localhost,1433`.
- [ ] Every remote Cashier uses the Server PC computer name or a reserved IP.
- [ ] Server PC sleep and hibernation are disabled during store hours.
- [ ] SQL Server service is Running and Automatic.
- [ ] SQL Server TCP 1433 is reachable from each Cashier.
- [ ] Temporarily stop SQL Server and confirm Cashier displays the recovery window.
- [ ] Restart SQL Server and confirm **Retry Connection** continues to login.
- [ ] Open **Repair or Configure Advanced POS Cashier** and confirm existing values load.
- [ ] Save a diagnostic report and confirm it does not contain the SQL password.
- [ ] Restart the router and confirm remote Cashiers reconnect after the LAN returns.

## Version 1.0.3 setup and upgrade acceptance

- [ ] Deployment wizard fits inside the Windows work area.
- [ ] **Start Setup** and **Close** remain visible while the setup form scrolls.
- [ ] Existing valid database automatically selects **Upgrade or repair existing store**.
- [ ] New Store safely refuses an existing database or login.
- [ ] Upgrade/Repair creates a verified pre-upgrade backup before migrations.
- [ ] Existing users, items, stock, sales, licences, settings, and terminal assignments remain unchanged after upgrade.
- [ ] Missing restricted application login is recreated safely for a valid Advanced POS database.
- [ ] Unknown, empty, inaccessible, or newer databases are refused without modification.
- [ ] Re-running Upgrade/Repair is idempotent and succeeds without duplicating data.
