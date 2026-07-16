# POS Network Edition Runtime Checklist

## Server PC

- SQL Server Express service starts automatically.
- TCP port is listening.
- Firewall rule is enabled for Private networks only.
- BackOffice connects to the central database.
- Existing store, user, supplier, customer, item, stock, sales, shift, voucher, tax, return, and ledger data are present.
- A SQL Server backup completes and passes `RESTORE VERIFYONLY`.
- The disposable rehearsal performs a real backup restore drill before production installation.

## Each Cashier terminal

- Server TCP connection succeeds.
- The encrypted connection profile is created for the actual Windows cashier account.
- Terminal registration and terminal licence match the machine.
- Login, shift open, item lookup, stock inquiry, hold/recall, payment, receipt, return, and shift close are tested.
- Printer and cash drawer settings are local to the terminal.

## Multi-terminal gate

- Two terminals can search and sell simultaneously.
- Concurrent last-stock sale never creates negative stock.
- The same checkout token creates only one financial effect.
- A Gift Voucher is redeemed only once.
- Customer credit cannot exceed its limit under concurrency.
- Document numbers remain unique.
- Server or cable interruption prevents partial financial posting.
