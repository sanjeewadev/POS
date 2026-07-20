# Phase 11D.3 — Store Connection Resilience and Recovery

**Release:** Advanced POS 1.0.2
**Baseline:** `c1187ece754ea51c5fe0688553ae9d129128afe5`
**Scope:** deployment, connection recovery, diagnostics, and SQL Server service resilience
**Database schema:** unchanged

## Purpose

Phase 11D.3 reduces avoidable store stoppages caused by temporary SQL Server startup delays, router replacement, DHCP address changes, damaged local connection profiles, and technician difficulty repairing an installed Cashier.

It does not add offline Cashier sales or database synchronization. A central-database Cashier still requires the Server PC, SQL Server, and local store network to be available.

## Controlled changes

- Cashier and BackOffice make three controlled SQL Server startup attempts before reporting failure.
- Cashier displays a recovery window with Retry, Repair Connection, Save Diagnostics, Restart Cashier, and Exit actions.
- Connection diagnostics redact the SQL password and record profile, name-resolution, adapter, and TCP endpoint results.
- The Cashier configuration launcher searches the current installation and the standard Server/Cashier installation folders.
- The deployment wizard reloads the existing DPAPI-protected database profile when repairing the same Windows user installation.
- Existing terminal number and name are reloaded from the deployment record when available.
- Server, BackOffice, and same-computer Cashier profiles use `localhost`, preventing router or LAN IP changes from breaking local operation.
- A verified legacy same-computer Server installation automatically converts its matching old LAN-IP BackOffice/Cashier profile to `localhost`; developers can suppress this with `POS_DISABLE_LOCAL_SERVER_PROFILE_RECOVERY=1`.
- Remote Cashier setup now recommends the stable Server PC computer name before a numeric IP address.
- Server deployment records the server computer name, current LAN address, and recommended remote Cashier host.
- Server-side status and restore tools always use `localhost`, so an old recorded LAN address cannot break local recovery operations.
- SQL Server is set to Automatic startup with three one-minute service-restart actions.
- The installed Cashier Start-menu shortcut is named **Repair or Configure Advanced POS Cashier**.
- Product and installer version defaults advance to `1.0.2`.

## Recovery behavior

### Temporary outage

1. Advanced POS attempts the connection three times.
2. The recovery window opens if all attempts fail.
3. Restore server/network availability.
4. Select **Retry Connection**.
5. Cashier continues without restarting when the original profile is valid again.

### Router, server name, IP, or port changed

1. Select **Repair Connection**.
2. The existing encrypted profile values are loaded into the wizard.
3. Enter the current Server PC computer name or IP address.
4. Complete terminal verification.
5. Return to the recovery window and select **Restart Cashier**.

The repair does not delete the database, historical transactions, terminal assignment, or licence. A terminal number already assigned to another computer remains protected by the existing provisioning rules.

## Deployment rules

### One-computer store

- Install Server, BackOffice, and Cashier on the same PC.
- The saved database endpoint is `localhost,1433`.
- Internet, router, Wi-Fi, and LAN IP changes do not affect the local database connection.
- SQL Server must still be running.

### Multi-computer store

- Keep the Server PC computer name stable.
- Enter that computer name on each remote Cashier when name resolution works.
- Configure a router DHCP reservation for the Server PC as a backup operational control.
- Prefer wired Ethernet and protect the Server PC, router, and switch with a UPS.
- Disable sleep during store hours.

## Boundaries

This phase cannot provide operation during:

- Server PC power or hardware failure;
- complete LAN failure;
- SQL Server failure that Windows cannot restart;
- database corruption;
- catastrophic disk loss.

Those conditions require infrastructure repair or database restore. Offline multi-master Cashier synchronization is intentionally outside Version 1.0 because it would introduce stock, receipt-number, returns, customer-credit, voucher, shift, and payment reconciliation risks.

## Release validation

Before customer use:

1. Build Debug and Release.
2. Run the full Core regression suite.
3. Run SQLite and SQL Server Cashier audit suites.
4. Compile both Inno Setup installers as version `1.0.2`.
5. Upgrade Server first, then Cashiers.
6. Confirm the server profile uses `localhost`.
7. Confirm remote Cashier uses the server computer name or reserved IP.
8. Stop and restart SQL Server and verify recovery.
9. Restart the router and both computers.
10. Complete a sale, verify stock in BackOffice, and create a database backup.
