# Manual Backup / Restore Hotfix 1

This hotfix replaces the unsupported `GetMigrationsAsync()` call with the
supported synchronous `GetMigrations()` EF Core API.

Extract this ZIP into:

`C:\Users\Sanjeewa\Dev\MyProjects\POS`

with overwrite enabled.

Then rebuild Debug and Release.

Do not test Restore until both builds have 0 errors.
