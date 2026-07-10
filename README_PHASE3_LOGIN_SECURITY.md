# Phase 3 Login Security Patch

This patch adds a simple offline login-protection design.

## Included

- Minimum password length changed from 12 to 6 characters.
- A password must contain at least one letter and one number.
- Special characters are optional.
- Five failed attempts lock the user account for 15 minutes.
- A successful login clears the failed-attempt counter.
- Login success, failure, suspended-account attempts, and lockout attempts
  are written to the local `LoginAuditEvents` table.
- Passwords and password hashes are never written to the audit table.
- Existing longer passwords continue to work.

## Database changes

A new EF migration is required. Expected model changes:

- Add `Users.FailedLoginAttempts`
- Add `Users.LockoutEndUtc`
- Add `Users.LastLoginAtUtc`
- Create `LoginAuditEvents`

Do not run BackOffice or Cashier until the generated migration has been
reviewed.
