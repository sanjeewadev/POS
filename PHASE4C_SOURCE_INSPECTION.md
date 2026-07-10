# Phase 4C Source Inspection

## Current behavior confirmed

- BackOffice File → Exit shuts down the application but does not explicitly
  clear the authenticated session.
- The BackOffice window X closes directly without a common confirmation path.
- There is no separate Logout command in the current management menu.
- The existing login window uses a different visual style from the newer
  compact classic BackOffice pages.
- The first-run Administrator window also uses a different visual style.
- BackOffice startup and login currently display raw exception messages in
  some failure paths.
- Cashier startup also displays raw exception details for database failures.
- Neither application has one shared local exception log for UI,
  application-domain, and unobserved task exceptions.

## Final Phase 4C design

- No separate BackOffice Logout button.
- File → Exit and the window X both:
  - ask for confirmation;
  - clear the in-memory authenticated session;
  - close BackOffice completely.
- BackOffice does not reopen another login window during exit.
- Login and first-run Administrator setup use the preferred compact classic
  page style.
- Both BackOffice and Cashier write local technical logs under:
  `%LocalAppData%\POS\Logs`
- Store users receive friendly messages instead of raw stack traces.
- No cloud logging, telemetry, background upload, or database migration.
