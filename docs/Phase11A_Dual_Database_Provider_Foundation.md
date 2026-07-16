# Phase 11A — Dual Database Provider Foundation

## Purpose

Phase 11A prepares the existing POS applications to run in either of two controlled database modes without changing business workflows:

- standalone SQLite for BackOffice and Cashier on one Windows computer;
- central SQL Server for a future LAN deployment with one BackOffice/server computer and one or more Cashier terminals.

## Implemented foundation

- Added the EF Core SQL Server provider while retaining the existing SQLite provider.
- Added a validated database-provider profile with explicit SQL Server TCP host and port settings.
- Stored the local connection profile with Windows DPAPI encryption for the current Windows user.
- Preserved SQLite as the default when no profile exists.
- Centralized provider configuration for `AppDbContext` and both application startup paths.
- Kept automatic EF migrations only for standalone SQLite.
- Made central startup verify server connectivity and a stable core schema table without attempting to migrate SQL Server.
- Replaced repository queries that hard-coded SQLite `NOCASE` with centralized provider-aware collations while preserving existing case-insensitive behavior.
- Made model collations provider-aware: `NOCASE` for SQLite and `Latin1_General_100_CI_AS_SC` for SQL Server.
- Made explicit large-text mappings provider-aware: `TEXT` for SQLite and `nvarchar(max)` for SQL Server.
- Required encrypted SQL Server transport for every accepted central profile.
- Disabled the legacy local SQLite backup/restore implementation when central SQL Server mode is selected.
- Added calculation and source-policy regression checks for the provider foundation.

## Safety boundaries

Phase 11A does **not** make the Network Edition ready for a store installation. It deliberately does not include:

- SQL Server installation or database creation;
- the approved SQL Server schema baseline and upgrade utility;
- the store connection setup screen;
- SQL Server backup and restore;
- multi-computer LAN and failure testing;
- final deployment installers.

The existing SQLite migration chain remains the authoritative migration path for standalone mode and must not be applied directly to SQL Server.

## Verification gates

The phase is accepted only after all of the following pass on Windows:

1. clean Git baseline verification;
2. patch dry-run and indexed application;
3. `git diff --cached --check`;
4. package restore;
5. Debug solution build;
6. all core calculation/regression checks;
7. all Cashier audit/source-policy checks;
8. empty SQLite migration smoke test;
9. Release solution build.
