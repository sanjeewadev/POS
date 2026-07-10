# Advanced POS System — Recovery Baseline

**Recorded date:** 10 July 2026  
**Current recovery stage:** Phase 0 completed sufficiently to begin Phase 1  
**Purpose:** Preserve the known starting condition of the project before code, database, security, VAT, or architecture changes are made.

---

## 1. Project Summary

The Advanced POS System is an existing application under development.

### Technology

- C#
- .NET 8
- WPF
- MVVM
- Visual Studio 2022
- Entity Framework Core
- SQLite

### Main solution

`C:\Users\Sanjeewa\Dev\MyProjects\POS\POS.sln`

### Main source folder

`C:\Users\Sanjeewa\Dev\MyProjects\POS`

### Private tools folder

`C:\Users\Sanjeewa\Dev\MyProjects\POS.PrivateTools`

### Original source archives

- `C:\Users\Sanjeewa\Dev\MyProjects\POS.zip`
- `C:\Users\Sanjeewa\Dev\MyProjects\POS.PrivateTools.zip`

The original ZIP files are immutable reference copies and must not be edited or overwritten.

---

## 2. Development Computer

### Hardware

- Device name: `LAPTOP-NGT2KF34`
- Manufacturer: HP
- Model: HP Laptop 15-fd0xxx
- Processor: 13th Gen Intel Core i5-1335U, 1.30 GHz
- Installed RAM: 8.00 GB
- Usable RAM: 7.65 GB
- System type: 64-bit operating system, x64-based processor
- Graphics: Intel UHD Graphics
- Storage: 477 GB total
- Storage used when recorded: approximately 273 GB
- Pen and touch: unavailable

### Windows

- Edition: Windows 11 Home Single Language
- Version: 25H2
- OS build: 26200.8655
- Installation date: 21 November 2025
- Windows Feature Experience Pack: 1000.26100.315.0

---

## 3. Visual Studio Environment

- Product: Microsoft Visual Studio Community 2022 (64-bit)
- Channel: Current
- Edition: Community
- Version: 17.14.33
- Release period: May 2026
- Full release identifier: `VisualStudio.17.Release/17.14.33+37314.3.-may.2026-`
- Microsoft .NET Framework shown by Visual Studio: 4.8.09221
- C# Tools: 4.14.0-3.26229.7+2b68181e46362f449af2b69df2d73fe87873b840
- NuGet Package Manager: 6.14.3
- GitHub Copilot: 17.14.1681.23550
- Visual Studio IntelliCode: 2.2
- SQL Server Data Tools: 17.14.26.0
- ASP.NET and Web Tools: 17.14.150.18679
- Azure App Service Tools: 17.14.150.18679
- Azure Functions and Web Jobs Tools: 17.14.150.18679
- Razor: 17.14.3.2530601+3372435431977e91904a23ceb1eab689badc1bd9
- TypeScript Tools: 17.0.40502.2001
- Visual Studio Tools for Unity: 17.14.1.0

### Still to verify during Phase 1

- Installed .NET SDK versions
- Installed .NET runtime versions
- NuGet package sources
- Exact build configuration
- Current Git branch
- Current Git commit
- Whether a Git remote exists

The .NET Framework version shown in Visual Studio is not the same as the .NET 8 SDK. The installed .NET 8 SDK must be checked separately.

---

## 4. Current Operational Status

The application is currently described as under development.

No real production deployment has been confirmed.

Until confirmed otherwise:

- Production Cashier executable: none confirmed
- Production BackOffice executable: none confirmed
- Production database: none confirmed
- Production customer licenses: none confirmed
- Production printer configuration: none confirmed
- Production installer: not yet created
- Production deployment method: not yet decided

### Development database

Expected location:

`%LocalAppData%\POS\pos_local.db`

Expanded expected location:

`C:\Users\Sanjeewa\AppData\Local\POS\pos_local.db`

The current database should be treated as a development/test database unless a real production deployment is later identified.

---

## 5. Phase 0 Work Already Completed

- [x] Original `POS.zip` preserved
- [x] Original `POS.PrivateTools.zip` preserved
- [x] Current database backed up
- [x] Existing configuration, license, receipt-template, report, and installation files backed up where present
- [x] Main source extracted to the working project folder
- [x] Git repository created for the main POS source
- [x] Source inspection report preserved
- [x] Source file inventory preserved
- [x] Recovery baseline documentation created

---

## 6. Recovery Rules

### Source protection

- Do not modify the original ZIP files.
- Do not overwrite the preserved database backup.
- Do not perform recovery work inside the ZIP files.
- Work only in the extracted Git-controlled project.
- Treat the current repository files as the source of truth.
- Do not use older ChatGPT or Gemini code when it conflicts with the repository.

### Database protection

- Do not delete the `Migrations` folder.
- Do not delete `AppDbContextModelSnapshot`.
- Do not recreate all migrations before inspecting the current database.
- Do not run `Database.EnsureDeleted()` against an important database.
- Do not test database changes against the only database copy.
- Do not rename tables or columns without schema analysis.
- Do not remove legacy fields merely because they appear unused.
- Do not replace the current database with a newly generated empty database.

### Code-change rules

- Identify the root cause before changing code.
- Do not perform large automatic rewrites.
- Do not replace many files in one step.
- Do not redesign the architecture unless explicitly approved.
- Do not delete duplicate-looking code until its usage is checked.
- Do not remove commented code only because it appears old.
- Make one logical correction at a time.
- Build and test after each logical correction.
- Record every changed file.
- Use a separate Git commit for each completed recovery task.
- Inspect dependent files before generating replacement code.

---

## 7. Licensing Security Record

The inspected private License Generator contains an RSA private key:

`POS.PrivateTools\POS.LicenseGenerator\POS.LicenseGenerator\Keys\private_key.pem`

Its matching public key is:

`POS.PrivateTools\POS.LicenseGenerator\POS.LicenseGenerator\Keys\public_key.pem`

### Status

The old private key is permanently retired from future production use.

### Reason

The private key was included in the project archive. It must therefore be treated as exposed and unsuitable for production licensing.

### Temporary licensing rules

- Do not generate customer or production licenses with the old private key.
- Do not install the matching old public key into the production POS.
- Do not distribute the License Generator to customers.
- Do not publish the private tools project publicly.
- Do not send the private key through email, messaging, cloud storage, or AI services.
- Keep the original private-tools ZIP only as a restricted historical reference.
- Create a new production key later, after secure storage and license versioning are designed.

### Existing production licenses

None have been confirmed.

---

## 8. Important Findings from the Source Inspection

The following issues were identified before any recovery changes:

- Database initialization uses `EnsureCreated()` even though migrations exist.
- The current EF model and migration snapshot do not match.
- BackOffice contains conflicting login/startup behavior.
- BackOffice navigation has missing or inconsistent routes.
- VAT is not implemented end to end.
- Sales records do not contain complete VAT snapshots or totals.
- A hard-coded `sa` / `sa123` authentication bypass exists.
- The POS license verifier still contains a placeholder public key.
- Cashier startup does not consistently enforce licensing.
- Dependency-injection registrations contain duplicates or conflicts.
- Some Cashier and BackOffice workflows are incomplete.
- Terminal, printer, store, receipt, and label values are partly hard-coded.
- No automated test project was found.
- The main checkout process contains meaningful transaction logic and should be stabilized rather than replaced without analysis.

---

## 9. Development Gates

Do not begin unrelated feature development until:

- The solution builds reproducibly.
- The database and migration state are understood.
- The administrator bypass is removed.
- Startup behavior is deterministic.
- Licensing is either correctly implemented or deliberately disabled.
- Store, terminal, printer, and database configuration sources are defined.

Do not change sales, invoice, payment, stock, or VAT behavior until:

- The database upgrade strategy is proven.
- VAT rules are approved.
- Existing checkout behavior is protected by automated tests.

---

## 10. Phase 1 Objective

The next phase is:

**Phase 1 — Establish a reproducible build baseline**

The first actions are:

1. Open the untouched `POS.sln` in Visual Studio.
2. Confirm the installed .NET SDK.
3. Restore NuGet packages.
4. Build the complete solution without changing code.
5. Capture the full Build Output.
6. Capture the full Error List.
7. Record all warnings and errors before applying fixes.
8. Fix only build, package, SDK, and reference blockers first.

---

## 11. Current Status

Phase 0 is sufficiently complete to begin Phase 1.

No business logic, database schema, sales logic, payment logic, VAT logic, or application architecture was intentionally changed during Phase 0.
