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
