# Phase 11D.4 — Production Setup, Upgrade, Repair, and Wizard Layout Hardening

**Release:** Advanced POS 1.0.3
**Database schema:** unchanged
**Primary objective:** make fresh installation, existing-store upgrade, repair, and setup recovery explicit, safe, repeatable, and usable at common Windows display scaling levels.

## Changes

- Added non-destructive SQL installation inspection.
- Added explicit **Upgrade or repair an existing Advanced POS store** mode.
- Existing valid databases automatically select Upgrade/Repair.
- Upgrade/Repair verifies database identity and integrity, creates a pre-upgrade backup, applies pending migrations, repairs the restricted application login and encrypted profile, verifies application access, and creates a post-upgrade backup.
- New Store reports controlled errors for existing databases, missing-login states, and leftover logins instead of `UNEXPECTED_SETUP_FAILURE`.
- Unknown, empty, inaccessible, and newer databases fail closed without modification.
- Setup remains idempotent for a valid existing installation.
- The deployment wizard is clamped to the Windows work area.
- The scrollable form body and fixed action footer are separated so **Start Setup** and **Close** remain visible.
- Permanent Server and Cashier Inno Setup AppIds are unchanged.

## Preserved information

Upgrade/Repair preserves the production database, store data, users, stock, sales, reports, licences, terminal identities, machine registrations, encrypted connection profiles, hardware settings, deployment reports, and backups.

## Exclusions

This phase does not add offline Cashier selling, database failover, cloud synchronization, schema redesign, or automatic deletion of partial SQL resources.
