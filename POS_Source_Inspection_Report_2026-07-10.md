# Advanced POS System — Source Inspection Report

**Inspection date:** 10 July 2026  
**Source inspected:** `POS.zip`  
**Technology:** C#, .NET 8, WPF, MVVM, Entity Framework Core, SQLite  
**Inspection mode:** Read-only static source inspection  

## Scope and evidence status

No source file was modified, rewritten, generated, or replaced. The ZIP was extracted to a temporary read-only working copy for inspection.

This report uses the following evidence labels:

- **Verified:** Directly confirmed from the files in the ZIP.
- **Static inference:** Strong conclusion from source structure, but not executed in the running application.
- **Not verified:** Requires compilation, runtime access, a real database, hardware, external files, or business rules not present in the ZIP.

The environment used for inspection did not contain `dotnet`, `MSBuild`, or a C# compiler. Therefore, the solution was **not compiled** and the report does not claim that it currently builds or runs.

---

# Executive assessment

The solution already contains a broad POS implementation rather than an empty prototype. It includes inventory masters, purchasing, GRN, stock, suppliers, customers, cashier sales, split tendering, gift vouchers, discounts, free issues, supplier claims, reporting screens, settings, backup, terminals, and licensing structures.

However, it is not yet safe to treat as a production-ready final system. The highest-risk confirmed issues are:

1. **Database creation and migration are inconsistent.** Both executables call `Database.EnsureCreated()` although the project contains migrations. The current EF model and migration snapshot have materially diverged.
2. **BackOffice authentication/startup has two competing login flows.** A modal login succeeds, then the newly resolved `MainViewModel` initializes another `LoginViewModel` as the shell page.
3. **VAT is only partially implemented.** PO and GRN contain VAT calculations, but they do not use the `TaxRate` master as the authoritative source. Sales headers and lines do not contain tax snapshots or VAT totals, and receipt output is not a tax-invoice implementation.
4. **A hard-coded super-administrator credential exists in production source.** `sa` / `sa123` bypasses the database user system.
5. **Licensing is structurally present but not operational.** The embedded public key is a placeholder, and Cashier startup does not enforce the licensing service.
6. **Several visible workflows are stubs or incomplete.** Lock screen, returns, Z report, some exports, reports, and cloud sync are examples.
7. **Architecture is mixed.** MVVM, view-first navigation, service locator access, code-behind orchestration, and direct `MessageBox` usage coexist.
8. **There is no automated test project.** Transactional checkout code is significant, but no unit or integration test safety net is present.

A recovery should begin with a reproducible build and database baseline before any feature work or broad refactoring.

---

# 1. Complete solution and project structure

## 1.1 Solution

**Verified:** `POS.sln` contains four projects:

| Project | Type | Target | Role |
|---|---|---:|---|
| `POS.BackOffice.UI` | WPF `WinExe` | `net8.0-windows` | Management, inventory, purchasing, CRM, reports, settings, administration |
| `POS.Cashier.UI` | WPF `WinExe` | `net8.0-windows` | Login, shift handling, sales screen, tendering, cashier dialogs |
| `POS.Core` | Class library | `net8.0-windows` | EF data model, entities, repositories, authentication, backup, licensing, shared interfaces |
| `POS.Hardware` | Class library | `net8.0-windows` | Hardware-facing receipt printer implementation |

## 1.2 Source counts

Counts exclude `.git`, `bin`, and `obj`:

| Project | C# files | XAML files |
|---|---:|---:|
| `POS.BackOffice.UI` | 100 | 48 |
| `POS.Cashier.UI` | 61 | 37 |
| `POS.Core` | 158 | 0 |
| `POS.Hardware` | 1 | 0 |
| **Total** | **320** | **85** |

A separate complete path inventory is supplied with this report as `POS_Source_File_Inventory_2026-07-10.txt`.

## 1.3 Directory structure

```text
POS.sln
├── POS.BackOffice.UI
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── Converters
│   ├── Dialogs                    [declared folder; no active source files]
│   ├── Resources
│   ├── Services
│   ├── ViewModels
│   └── Views
│       ├── Dialogs
│       ├── Layout
│       └── Pages
│           ├── Admin
│           ├── Crm
│           ├── File
│           ├── Finance
│           ├── InventoryOperations
│           ├── InventoryPages
│           ├── Purchasing
│           ├── Reports
│           └── Sales
├── POS.Cashier.UI
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── Components
│   ├── Dialogs
│   ├── Messages
│   ├── Models
│   ├── Resources
│   ├── Services
│   ├── ViewModels
│   └── Views
├── POS.Core
│   ├── Data
│   ├── Enums
│   ├── Interfaces
│   ├── Migrations
│   ├── Models
│   │   ├── Backup
│   │   ├── DTOs
│   │   ├── Licensing
│   │   └── Terminals
│   ├── Repositories
│   ├── Services
│   │   ├── Backup
│   │   └── Licensing
│   ├── Sync
│   └── Utilities
└── POS.Hardware
    ├── Barcode                     [empty]
    ├── CashDrawer                  [empty]
    ├── Printers                    [empty]
    └── Services
```

## 1.4 Project dependencies and packages

### `POS.BackOffice.UI`

**Verified:** References `POS.Core` and `POS.Hardware`. Important packages:

- `CommunityToolkit.Mvvm 8.4.2`
- `LiveCharts.Wpf 0.9.7`
- `Microsoft.EntityFrameworkCore.Design 8.0.0`
- `Microsoft.EntityFrameworkCore.Sqlite 8.0.28`
- `Microsoft.Extensions.DependencyInjection 8.0.1`
- `Microsoft.Xaml.Behaviors.Wpf 1.1.142`
- `ZXing.Net.Bindings.Windows.Compatibility 0.16.14`

`NU1701` is suppressed in this project. This is likely related to an older-framework package such as `LiveCharts.Wpf`, but the exact restore warning was **not verified** because restore was not run.

### `POS.Cashier.UI`

**Verified:** References `POS.Core` and `POS.Hardware`. Packages:

- `CommunityToolkit.Mvvm 8.4.2`
- `Microsoft.EntityFrameworkCore.Design 8.0.6`

The project file excludes two source paths that do not exist in the ZIP:

- `ViewModels/IssueVoucherModalViewModel.cs`
- `ViewModels/RedeemVoucherModalViewModel.cs`

These are stale project exclusions rather than active missing compile inputs.

### `POS.Core`

**Verified:** Packages:

- `CommunityToolkit.Mvvm 8.4.2`
- `Microsoft.EntityFrameworkCore 8.0.28`
- `Microsoft.EntityFrameworkCore.Sqlite 8.0.28`
- `Microsoft.EntityFrameworkCore.Tools 10.0.9`

The EF Tools package is major version 10 while runtime packages and target framework are version 8. This is a **verified version mismatch**. Whether it currently causes restore/build failure is **not verified**.

### `POS.Hardware`

**Verified:** References `POS.Core`; package `System.Drawing.Common 8.0.28`. Only one active C# implementation exists: `Services/ReceiptPrinterService.cs`.

---

# 2. Purpose of each major folder

## `POS.BackOffice.UI`

- **Converters:** WPF value converters used by bindings.
- **Resources:** Shared colour/style dictionaries and UI resources.
- **Services:** UI-specific abstractions and implementations, including message-box and barcode printing services.
- **ViewModels:** BackOffice page state, commands, validation, repository calls, and navigation root.
- **Views/Layout:** Main management shell.
- **Views/Pages/InventoryPages:** Item, category, subcategory, attribute/property, supplier, UOM, and tax masters.
- **Views/Pages/InventoryOperations:** GRN, stock, returns, express items, barcode, and price workflows.
- **Views/Pages/Purchasing:** Purchase order creation and dashboard.
- **Views/Pages/Finance:** Supplier ledger and claims.
- **Views/Pages/Crm:** Customers, customer ledger, gift vouchers, free-issue rules.
- **Views/Pages/Sales:** Sales explorer, receipts, returns audit, item analytics, security audit.
- **Views/Pages/Reports:** Financial, supplier, cash movement, and float-cash reporting.
- **Views/Pages/Admin:** Users, terminals, settings, backups, licensing, suspended transactions.
- **Views/Pages/File:** Terminal settings page.
- **Views/Dialogs:** At least the variant-to-supplier assignment dialog.

## `POS.Cashier.UI`

- **Views:** Login, main sales screen, and terminal-locked view.
- **Dialogs:** Tendering, customer search/creation, manager authorisation, stock/product search, discounts, vouchers, shift, returns, suspended carts, lock screen, and reports.
- **ViewModels:** Main cashier sales orchestration and selected dialog workflows.
- **Models:** Cashier-only presentation models such as cart/payment representations.
- **Messages:** Messenger payloads used with CommunityToolkit weak-reference messaging.
- **Services:** ESC/POS printing and cashier-specific receipt-print abstraction.
- **Components / Resources:** Reusable UI components and resources.

## `POS.Core`

- **Data:** `AppDbContext` and EF model configuration.
- **Models:** Persistent entities and shared data structures.
- **Models/DTOs:** Query and UI transfer shapes used by repositories/ViewModels.
- **Models/Backup, Licensing, Terminals:** Domain-specific model groups.
- **Repositories:** Concrete EF-based data access and business transaction classes.
- **Services:** Authentication and cross-cutting services.
- **Services/Backup:** Database backup workflow.
- **Services/Licensing:** Fingerprint, signature, file, and licence-management logic.
- **Interfaces:** Shared printer abstraction.
- **Migrations:** EF Core migration history and model snapshot.
- **Enums:** Domain enums; one filename is misspelled as `TransationType.cs` while the type is `TransactionType`.
- **Utilities:** Shared helpers, including password/security utilities.
- **Sync:** A completely commented-out cloud-sync worker from an older namespace/design.

## `POS.Hardware`

- **Services:** Receipt-printer service.
- **Barcode, CashDrawer, Printers:** Declared but empty; there are no active specialised implementations under these folders.

---

# 3. Application startup flow

## 3.1 BackOffice startup

**Verified flow:**

1. `App` constructor builds the DI container (`POS.BackOffice.UI/App.xaml.cs`).
2. A SQLite database path is constructed at `%LocalAppData%\POS\pos_local.db`.
3. `Application_Startup` creates an `AppDbContext` and calls `Database.EnsureCreated()` (`App.xaml.cs:203-213`).
4. Shutdown mode is temporarily set to `OnExplicitShutdown`.
5. A transient `LoginViewModel` is resolved and passed to modal `LoginWindow`.
6. When `ShowDialog()` returns `true`, `ManagementShellView` is created.
7. The singleton `MainViewModel` is resolved and assigned as shell `DataContext`.
8. Shutdown mode changes to `OnLastWindowClose`, and the shell is shown.

### Confirmed startup conflict

`MainViewModel` calls `LoadLoginScreen()` in its constructor. That method resolves a **new** transient `LoginViewModel`, subscribes to its event, and assigns it to `CurrentPage` (`MainViewModel.cs:32-49`). This occurs after the modal login has already succeeded.

There is no `DataTemplate` for `LoginViewModel` in BackOffice `App.xaml`.

**Static inference:** On successful modal login, the shell will begin with a second, unrelated login object in `CurrentPage`, rather than a normal management page. The exact visual result—blank content, type-name text, or another fallback—requires runtime verification.

## 3.2 Cashier startup

**Verified flow:**

1. `App` constructor builds DI.
2. `OnStartup` creates the same SQLite database path and calls `Database.EnsureCreated()`.
3. `TillRepository.GetActiveShiftAsync("01")` is synchronously blocked with `.GetAwaiter().GetResult()`.
4. A transient `LoginViewModel` is initialized with the active-shift state.
5. `LoginView` is shown.
6. On successful authentication, if there is no open shift, the application silently creates one with terminal `"01"` and opening cash implied as zero.
7. `SalesView` is created and shown; login closes.
8. `SalesView` resolves its own `SalesViewModel` through the global `App.Services` service locator.

### Startup concerns

- Terminal number `"01"` is hard-coded in startup.
- Blocking an asynchronous database call on the UI startup thread can freeze or deadlock under some conditions.
- A new shift is silently created without an explicit opening-cash workflow.
- Cashier startup does not call `LicenseManagerService`, despite BackOffice comments stating Cashier should enforce licensing.

---

# 4. MVVM structure

## 4.1 Framework and conventions

**Verified:**

- CommunityToolkit.Mvvm is used across both UIs.
- ViewModels commonly derive from `ObservableObject` or BackOffice `ViewModelBase`.
- `[ObservableProperty]` and `[RelayCommand]` generate properties and commands.
- BackOffice also contains a custom `RelayCommand` class.
- `ViewModelBase` is currently a thin/empty common base.

## 4.2 BackOffice pattern

The dominant pattern is **ViewModel-first navigation**:

- `MainViewModel.CurrentPage` stores the selected object.
- `ManagementShellView` binds a `ContentControl` to `CurrentPage`.
- `App.xaml` maps ViewModel types to UserControls with `DataTemplate` resources.

The implementation is not uniform. Some routes assign ViewModels to `CurrentPage`; others resolve and assign View instances directly:

- `StoreSettingsView`
- `TerminalSettingsView`
- `BackupRestoreView`
- `LicenseManagementView`
- `TerminalManagementView`

This creates a hybrid of ViewModel-first and View-first navigation.

## 4.3 Cashier pattern

Cashier is a hybrid MVVM/code-behind design:

- `SalesViewModel` contains substantial sales, cart, tender, pricing, voucher, free-issue, and checkout state.
- `SalesView.xaml.cs` contains extensive keyboard shortcut, dialog, inactivity, button, and screen-orchestration logic.
- Several dialogs have dedicated ViewModels.
- Several dialogs are code-behind-only and return primitive results or directly manipulate UI state.
- CommunityToolkit `WeakReferenceMessenger` is used for some interactions.

## 4.4 Architectural observations

**Verified:**

- Global `App.Services` / `GetRequiredService` is used from views and ViewModels.
- Direct `MessageBox` calls are common even though BackOffice defines `IMessageBoxService`.
- There is no central navigation service.
- There is no central dialog service.
- Most repositories are injected as concrete classes rather than interfaces.

**Risk:** This reduces isolation and testability, and makes object lifetimes/navigation behavior more difficult to reason about.

---

# 5. Models and important relationships

The current `AppDbContext` maps 49 entity types. Major relationships are summarized below.

## 5.1 Inventory master model

- `Category` → many `SubCategory`.
- `Category` ↔ `AttributeGroup` through `CategoryAttributeGroup`.
- `AttributeGroup` → many `AttributeValue`.
- `ItemParent` belongs to a category, optional subcategory, and unit of measure.
- `ItemParent` → many `ItemVariant`.
- `ItemVariant` → many `ItemBatch`.
- `ItemVariant` ↔ `Supplier` through `ItemSupplier`.
- `ItemPropertyMapping` connects a variant with attribute group/value selections.

`ItemParent` contains both newer configuration and legacy-compatible fields. It stores `TaxCode` as text and `IsTaxInclusive`, but it has no direct foreign key to `TaxRate`.

## 5.2 Purchasing and stock

- `PoHeader` → many `PoLine`; supplier → purchase orders.
- `PoLine` references `ItemVariant` and stores cost, discount, tax code, VAT rate, inclusion flag, tax amount, and line total.
- `GrnHeader` → many `GrnLine`; supplier → GRNs; a GRN may reference a PO.
- `GrnLine` references item variant, optional PO line, and generated/selected batch.
- `StockAdjustmentHeader` → many adjustment lines.
- `SupplierReturnHeader` → many return lines.
- `InventoryTransaction` records stock movements by item/batch.
- `SupplierLedger` records supplier-related financial movements.

## 5.3 Sales and cashier

- `ShiftSession` → cash movements; sales reference a shift session.
- `SalesHeader` → many `SalesLine`.
- `SalesHeader` → many `SalesPayment`.
- `SalesHeader` optionally references `CustomerMaster` and also stores customer snapshots.
- `SalesLine` optionally references item variant and batch; these may be null for non-stock voucher sale lines.
- `SalesLine` stores pricing, cost, discount, price-override, free-issue, supplier-claim, and discount-rule snapshots.
- `CustomerReturnHeader` → many return lines.
- `SuspendedTransaction` → suspended lines.
- `GiftVoucher` → voucher transactions.
- `SalesLineDiscountAudit` preserves rule/manual discount audit information.
- `FreeIssueRule`, `FreeIssueReason`, and `FreeItemClaimLog` support free-item workflows.

## 5.4 Settings, terminal, backup, and licensing

- `StoreSettings`
- `TerminalSettings`
- `RegisteredTerminal`
- `InstalledLicense`
- `BackupHistory`
- `DocumentSequence`
- `User`
- `TaxRate`
- `PriceChangeHistory`

## 5.5 Compatibility aliases and legacy model remnants

**Verified:** `AppDbContext` exposes the same supplier-return entity types under two DbSet name pairs:

- `ReturnHeaders` / `ReturnLines`
- `SupplierReturnHeaders` / `SupplierReturnLines`

A comment states the second pair is retained for existing repositories. This is compatibility duplication, not two separate domain entities.

Other models contain explicit legacy aliases or compatibility fields. These should not be removed until database and usage analysis is complete.

---

# 6. Views and corresponding ViewModels

## 6.1 BackOffice DataTemplate mappings

The following mappings are explicitly verified in `POS.BackOffice.UI/App.xaml`:

| ViewModel | View |
|---|---|
| `DashboardViewModel` | `DashboardView` |
| `CategoryViewModel` | `CategoryView` |
| `SubCategoryViewModel` | `SubCategoryView` |
| `ItemPropertyViewModel` | `ItemPropertyView` |
| `SupplierViewModel` | `SupplierView` |
| `ItemMasterViewModel` | `ItemMasterView` |
| `UnitOfMeasureViewModel` | `UnitOfMeasureView` |
| `TaxRateViewModel` | `TaxRateView` |
| `GrnViewModel` | `GrnView` |
| `GrnDashboardViewModel` | `GrnDashboardView` |
| `StockAdjustmentViewModel` | `StockAdjustmentView` |
| `StockBalanceViewModel` | `StockBalanceView` |
| `SupplierReturnViewModel` | `SupplierReturnView` |
| `ExpressItemAdminViewModel` | `ExpressItemAdminView` |
| `BarcodeManagementViewModel` | `BarcodeManagementView` |
| `BarcodePrinterViewModel` | `BarcodePrinterView` |
| `PriceManagementViewModel` | `PriceManagementView` |
| `PriceChangeHistoryViewModel` | `PriceChangeHistoryView` |
| `PurchaseOrderViewModel` | `PurchaseOrderView` |
| `PurchaseOrderDashboardViewModel` | `PurchaseOrderDashboardView` |
| `SupplierLedgerViewModel` | `SupplierLedgerView` |
| `SupplierClaimsViewModel` | `SupplierClaimsView` |
| `SupplierReportViewModel` | `SupplierReportView` |
| `FloatCashLogViewModel` | `FloatCashLogView` |
| `FinancialSummaryViewModel` | `FinancialSummaryView` |
| `CashMovementDashboardViewModel` | `CashMovementDashboardView` |
| `SalesExplorerViewModel` | `SalesExplorerView` |
| `SecurityAuditViewModel` | `SecurityAuditView` |
| `ItemSalesAnalyticsViewModel` | `ItemSalesAnalyticsView` |
| `CustomerReturnsAuditViewModel` | `CustomerReturnsAuditView` |
| `ReceiptLedgerViewModel` | `ReceiptLedgerView` |
| `CustomerMasterViewModel` | `CustomerMasterView` |
| `CustomerLedgerViewModel` | `CustomerLedgerView` |
| `GiftVoucherAdminViewModel` | `GiftVoucherAdminView` |
| `UserManagementViewModel` | `UserManagementView` |
| `SuspendedTransactionsMonitorViewModel` | `SuspendedTransactionsMonitorView` |
| `StoreSettingsViewModel` | `StoreSettingsView` |
| `TerminalSettingsViewModel` | `TerminalSettingsView` |
| `LicenseManagementViewModel` | `LicenseManagementView` |
| `TerminalManagementViewModel` | `TerminalManagementView` |
| `BackupRestoreViewModel` | `BackupRestoreView` |

### Orphaned BackOffice pair

`FreeIssueRuleSetupViewModel` and `FreeIssueRuleSetupView` both exist, and the ViewModel is registered in DI. However:

- no `DataTemplate` maps the ViewModel to the View;
- no `MainViewModel` navigation command routes to it;
- no shell menu reference was found.

The page is therefore statically inaccessible through the current central navigation flow.

## 6.2 Cashier primary mappings

- `LoginView` ↔ `LoginViewModel`
- `SalesView` ↔ `SalesViewModel`

Dialog/ViewModel pairs found in source:

| Dialog/View | ViewModel |
|---|---|
| `B2BCustomerDialogView`, `LoyaltyCustomerDialogView` | `B2BCustomerViewModel` |
| `BatchSelectionDialog` | `BatchSelectionViewModel` |
| `CardTenderDialog` | `CardTenderDialogViewModel` |
| `CashTenderDialog` | `CashTenderDialogViewModel` |
| `ChequeTenderDialog` | `ChequeTenderDialogViewModel` |
| `CashMovementDialogView` | `CashMovementViewModel` |
| `DiscountRuleDialog` | `DiscountRuleDialogViewModel` |
| `ExpressItemDialogView` | `ExpressMenuViewModel` |
| `FloatCashDialog` | `FloatCashViewModel` |
| `FreeItemReasonModalWindow` | `FreeItemReasonModalViewModel` |
| `GiftVoucherTenderDialog` | `GiftVoucherTenderDialogViewModel` |
| `ManagerAuthDialogView` | `ManagerAuthViewModel` |
| `ProductSeekDialog` | `PluSearchViewModel` |
| `QuickCustomerCreateDialog` | `QuickCustomerCreateViewModel` |
| `SellGiftVoucherDialog` | `SellGiftVoucherDialogViewModel` |

Code-behind-oriented dialogs include:

- `ConfirmDialogView`
- `Creditnote`
- `DiscountDialog`
- `HoldRecallDialog`
- `LockScreenView`
- `PriceDiscountDialog`
- `PriceOverrideDialog`
- `PrintOptionsDialog`
- `ReturnInvoiceDialog`
- `ShiftSummaryDialog`
- `StockInquiryDialog`
- `SuspendedCartsDialog`
- `ZReportSummaryDialog`
- `TerminalLockedView`

## 6.3 XAML structural check

**Verified by static scan:**

- Every active XAML file with `x:Class` has a matching `.xaml.cs` file.
- No named event-handler attribute was found without a corresponding method in its code-behind.

This is not equivalent to WPF compilation and does not validate bindings, generated command names, resource keys, or runtime DataContexts.

---

# 7. Services, repositories, interfaces, and commands

## 7.1 Repositories

`POS.Core/Repositories` contains these concrete repositories:

- `AttributeRepository`
- `BackupRepository`
- `BarcodeManagementRepository`
- `BarcodePrinterRepository`
- `CategoryRepository`
- `CustomerRepository`
- `DiscountRuleRepository`
- `ExpressItemRepository`
- `FinancialAnalyticsRepository`
- `FloatCashRepository`
- `FreeIssueRuleRepository`
- `FreeItemClaimRepository`
- `GiftVoucherRepository`
- `GrnHistoryRepository`
- `GrnRepository`
- `ItemMasterRepository`
- `LicenseRepository`
- `MasterSalesAnalyticsRepository`
- `PoRepository`
- `PriceChangeHistoryRepository`
- `PriceManagementRepository`
- `SalesAnalyticsRepository`
- `SalesRepository`
- `SecurityAuditRepository`
- `StockAdjustmentRepository`
- `StockBalanceRepository`
- `StoreSettingsRepository`
- `SubCategoryRepository`
- `SupplierLedgerRepository`
- `SupplierReportRepository`
- `SupplierRepository`
- `SupplierReturnRepository`
- `TaxRateRepository`
- `TerminalManagementRepository`
- `TerminalSettingsRepository`
- `TillRepository`
- `UnitOfMeasureRepository`
- `UserRepository`

Most repositories use `IDbContextFactory<AppDbContext>`, creating contexts per operation.

## 7.2 Services

Core services:

- `AuthService`
- `BackupService`
- `MachineFingerprintService`
- `LicenseSignatureService`
- `LicenseFileService`
- `LicenseManagerService`
- `IBarcodePrintService`

BackOffice UI services:

- `IMessageBoxService` / `MessageBoxService`
- `WpfBarcodePrintService`

Cashier UI services:

- `IReceiptPrintService` / `EscPosReceiptPrintService`

Hardware/shared printer path:

- `IReceiptPrinterService` in `POS.Core`
- `ReceiptPrinterService` in `POS.Hardware`

The two receipt-print abstractions overlap. The active checkout/quotation path uses Cashier's `IReceiptPrintService`; the Hardware `IReceiptPrinterService` is registered but no direct use in the examined checkout path was found.

## 7.3 Commands

- CommunityToolkit-generated commands from `[RelayCommand]` are the main command mechanism.
- BackOffice contains a separate custom `RelayCommand` implementation.
- Cashier also uses direct click/key handlers in code-behind.

### Confirmed command mismatch

`ManagementShellView.xaml` binds its Dashboard menu item to `NavigateToDashboardCommand`, but no `NavigateToDashboard` relay method or command property exists in `MainViewModel`. This command is therefore unresolved at runtime unless generated elsewhere, which was not found.

---

# 8. Dependency-injection configuration

## 8.1 BackOffice DI

**Verified:** `POS.BackOffice.UI/App.xaml.cs` registers:

- `IDbContextFactory<AppDbContext>` with SQLite.
- Singleton `IMessageBoxService`.
- Singleton `AuthService`.
- Singleton `MainViewModel`.
- Transient repositories.
- Transient feature ViewModels.
- Transient settings/licensing/backup Views for View-first navigation.
- Transient barcode, backup, fingerprint, licensing file/signature/manager services.

A static scan did not find a direct `GetRequiredService<T>` request for an unregistered type in the BackOffice application.

## 8.2 Cashier DI

**Verified:** Registers database factory, repositories, authentication, receipt printing, main/dialog ViewModels, gift voucher, free-issue, and discount-rule components.

### Duplicate registrations

- `FreeItemClaimRepository` is registered once as singleton and later as transient.
- `FreeItemReasonModalViewModel` is registered twice as transient.

For direct `GetRequiredService<T>`, Microsoft DI returns the last registration, so `FreeItemClaimRepository` will normally resolve as transient. Both registrations remain visible through `IEnumerable<T>`.

### Lifetime and design concerns

- `AuthService` is singleton and depends on transient `UserRepository`. The repository itself creates contexts through a factory, so this is less dangerous than capturing a scoped DbContext, but the lifetime intent is not explicit.
- Global static `App.Services` is used as a service locator.
- No scope validation or `ValidateOnBuild` configuration is enabled.
- Repository interfaces are generally absent, limiting test substitution.

A static constructor/dependency check did not identify an obvious unresolved DI constructor dependency. This does not replace container validation at runtime.

---

# 9. Database technology and configuration

## 9.1 Technology and location

**Verified:**

- Entity Framework Core with SQLite.
- Both UI executables use `%LocalAppData%\POS\pos_local.db`.
- `AppDbContext` also has a parameterless constructor and an `OnConfiguring` fallback to the same hard-coded location.
- There is no `appsettings.json`-based connection configuration in the solution.

## 9.2 Creation and migration behavior

Both applications call `Database.EnsureCreated()` and neither calls `Database.Migrate()`.

This has two important consequences:

1. A new database can be created directly from the current model without applying migration history.
2. An existing database is not upgraded by pending migrations.

The project contains 14 migrations plus `AppDbContextModelSnapshot`, dated from 13 June 2026 through 21 June 2026.

## 9.3 Confirmed model/snapshot drift

The current `AppDbContext` maps 49 entity types; the snapshot contains 43.

### Current model types absent from the snapshot

- `BackupHistory`
- `DiscountReason`
- `DiscountRule`
- `FreeIssueReason`
- `FreeIssueRule`
- `GiftVoucherTransaction`
- `InstalledLicense`
- `PriceChangeHistory`
- `RegisteredTerminal`
- `SalesLineDiscountAudit`
- `StoreSettings`
- `SupplierReturnHeader`
- `SupplierReturnLine`
- `TaxRate`
- `TerminalSettings`

### Snapshot types no longer present in the current model

- `LoyaltyDiscountProfile`
- `PromoCondition`
- `PromoReward`
- `PromoRule`
- `QuotationHeader`
- `QuotationLine`
- `ReturnHeader`
- `ReturnLine`
- `SystemSetting`

This is a material schema-history conflict. A database created by migrations and a database created by `EnsureCreated()` can have different structures.

## 9.4 Database state not inspected

The actual `%LocalAppData%\POS\pos_local.db` file was not included in the ZIP. Therefore, the following are unknown:

- current live schema;
- applied migration history;
- data integrity and duplicates;
- whether required tables/columns already exist;
- invoice sequence values;
- seeded users and settings;
- compatibility with the current model.

---

# 10. Navigation architecture

## 10.1 BackOffice

The shell uses a menu bound to `MainViewModel` commands and a `ContentControl` bound to `CurrentPage`.

### Verified navigation problems

- Startup creates the second login state described in section 3.
- Dashboard menu binds to a missing command.
- `FreeIssueRuleSetup` is registered but has no route/template.
- Settings/licence/backup pages use View instances, while most pages use ViewModels.
- `MainViewModel` alternates between its injected `_serviceProvider` and global `App.Services`.
- Some menu entries are Buttons embedded inside MenuItems, producing an inconsistent command structure.
- File-menu entries such as Backup/Exit appear without a complete command path.
- The shell status bar contains hard-coded display values, including user/date/version text.

## 10.2 Cashier

The top-level route is simple:

```text
LoginView → SalesView
```

Inside `SalesView`, navigation is modal and code-behind driven. There is no central navigation/dialog coordinator.

The inactivity behavior triggers a logoff route after a configured interval, while the lock-screen dialog itself is nonfunctional. Therefore, “lock” and “logoff” concepts are not consistently implemented.

---

# 11. Sales, invoice, payment, and VAT architecture

## 11.1 Sales/cart architecture

**Verified:** `SalesViewModel` maintains observable cart and payment-line collections and coordinates:

- barcode/SKU/product entry;
- batch selection and expiry-aware item sales;
- retail/wholesale customer selection and pricing;
- manual discounts and price overrides;
- rule-based discounts with approval snapshots;
- free issue and supplier-claim snapshots;
- gift voucher sale and redemption;
- split tendering;
- quotation printing;
- shift-aware checkout.

## 11.2 Checkout transaction

`SalesRepository.ProcessCheckoutAsync` performs checkout in an EF database transaction:

1. validates header, lines, payments, and active shift;
2. normalizes customer/line/payment data;
3. prevents buying a gift voucher with a gift voucher;
4. recalculates header totals;
5. validates payment totals and cash tendering;
6. obtains/increments `DocumentSequence` for invoice numbering;
7. persists sales header and lines;
8. validates and reduces batch stock;
9. records inventory movements;
10. writes discount audit rows;
11. records payments;
12. activates sold vouchers and records redemptions;
13. creates supplier claims for recoverable free items;
14. commits or rolls back the transaction;
15. reloads the saved receipt.

This is one of the most developed and coherent parts of the current codebase.

## 11.3 Invoice numbering

Invoice numbers come from `DocumentSequence.Prefix`, sequence value, and padding. `StoreSettings.InvoicePrefix` was not found in the checkout numbering path.

**Static inference:** Store-configured invoice prefix may not currently control sales invoice numbers. Runtime data and all sequence initialization paths should be checked before confirming this as user-visible behavior.

## 11.4 Payment architecture

Confirmed payment paths include:

- Cash
- Card
- Cheque
- Gift voucher

`SalesPayment` stores the payment record, while cashier presentation uses payment-line state for split tendering.

Other payment buttons/labels are present but at least one handler displays a “coming later” notification. Customer credit, credit note, bank transfer, or other methods should not be assumed complete solely from UI labels/comments.

## 11.5 Receipt and printing architecture

- Cashier sales/quotation output uses `EscPosReceiptPrintService` through `IReceiptPrintService`.
- `SalesViewModel` hard-codes printer name `POS-80`.
- Hardware `ReceiptPrinterService` has a separate abstraction/default path, creating overlapping printer architecture.
- Store details are partly hard-coded in print output rather than consistently loaded from `StoreSettings`.
- Quotation output explicitly prints `NOT A TAX INVOICE`.

## 11.6 VAT architecture: purchasing and GRN

PO and GRN models contain meaningful VAT data:

- tax code;
- VAT rate percent;
- inclusive/exclusive flag;
- line VAT amount;
- header VAT total.

Repositories calculate inclusive VAT by extracting tax from a tax-inclusive amount and exclusive VAT by adding tax to the discounted base. GRN landed-cost logic excludes claimable VAT from stock value and allocates freight/global discount.

### Confirmed TaxRate-master disconnect

`TaxRate` exists as a master entity and BackOffice page. However, `PoRepository` and `GrnRepository` use duplicated `ResolveVatRatePercent` methods that infer a rate from digits in the tax-code text:

- contains `18` → 18%
- contains `15` → 15%
- contains `12` → 12%
- contains `8` → 8%
- contains `5` → 5%
- otherwise → 0%

A code such as `VAT-STD` or `VAT` therefore resolves to zero unless a rate is separately supplied. Arbitrary rates defined in `TaxRate` are not authoritative in these repository paths.

`ItemParent` comments still describe Tax Master as a future addition, even though `TaxRate` now exists. This confirms incomplete integration between old and new implementations.

## 11.7 VAT architecture: sales

**Verified:**

- `SalesHeader` contains gross total, discount, net total, tendered amount, and balance—but no VAT total or taxable/tax-exempt breakdown.
- `SalesLine` contains price, discount, cost, totals, free issue, voucher, and audit fields—but no tax code/rate/inclusive flag/VAT amount snapshot.
- No VAT calculation was found in `SalesViewModel` or `SalesRepository`.
- `StoreSettings.GlobalVatRate` is used in settings CRUD/validation only, not in sales, PO, or GRN transaction calculations.
- Customer VAT registration information can be copied into customer-related snapshots, but this does not create a tax-invoice calculation model.

**Conclusion:** Sales VAT and tax-invoice architecture are not implemented end-to-end in the current source.

---

# 12. Duplicate, conflicting, incomplete, or suspicious implementations

## 12.1 Critical/security

### Hard-coded super-admin bypass

`AuthService` contains a “SKELETON KEY” path:

- Username: `sa`
- Password: `sa123`
- In-memory user ID: `0`
- Role: Admin

This bypasses database credentials and normal account state. It is a critical production security risk.

### Licensing placeholder

`LicenseSignatureService` embeds:

```text
REPLACE_THIS_WITH_YOUR_REAL_RSA_PUBLIC_KEY
```

Signature verification returns false while the placeholder remains. Cashier startup does not enforce licensing.

## 12.2 Startup/navigation conflicts

- Modal BackOffice login plus second `MainViewModel` login state.
- Missing Dashboard command.
- Orphaned Free Issue Rule setup route.
- Mixed View and ViewModel navigation.

## 12.3 Database conflicts

- `EnsureCreated()` used despite migrations.
- Current model/snapshot drift.
- EF Core Tools 10 with EF Core runtime 8.
- Duplicate DbSet aliases for supplier returns.
- Parameterless context fallback and UI-level connection configuration duplicate the database path.

## 12.4 DI/service duplication

- Duplicate `FreeItemClaimRepository` registration with conflicting lifetimes.
- Duplicate `FreeItemReasonModalViewModel` registration.
- Two overlapping receipt-printer interfaces/services.
- Both dependency injection and global service locator patterns.
- Both `IMessageBoxService` and direct static `MessageBox` usage.

## 12.5 Incomplete cashier workflows

- `LockScreenView` active handlers are empty; an older implementation is fully commented out.
- `ReturnInvoiceDialog` search and direct-item buttons have empty “Logic later” handlers; confirm merely closes with success.
- `SalesViewModel.ProcessZReportAsync()` returns `Task.CompletedTask`.
- At least one payment route displays “coming later”.
- Reports dialog displays “coming soon”.
- Terminal number is hard-coded to `01`.
- Printer name is hard-coded to `POS-80`.
- A second default printer name (`POS_Printer`) exists in the other printer implementation.
- Some async work is fire-and-forget, increasing unobserved-exception risk.

## 12.6 Incomplete BackOffice workflows

- Customer ledger PDF output is TODO.
- Customer receive-payment popup is TODO.
- Supplier claims export is TODO.
- Other report/export/print code contains placeholders or partial implementations.
- `LabelSettings` hard-codes `MY RETAIL STORE` with a TODO to load global settings.

## 12.7 Dead, legacy, or suspicious source

- `CloudSyncWorker.cs` is entirely commented out and references old namespaces/interfaces not present in the solution.
- Both UI projects contain empty default `MainWindow` files that are not part of the actual startup flow.
- Hardware feature folders are empty.
- Cashier `.csproj` contains stale exclusions for missing voucher modal ViewModels.
- `TransationType.cs` is misspelled.
- Comments and compatibility properties indicate several old/new model generations coexist.
- `.csproj.user` files are present in the archive even though `.gitignore` includes `*.user`.
- `.gitignore` contains plain separator/header text without comment markers, so those lines are interpreted as ignore patterns.

## 12.8 Source duplication check

A static scan did not identify duplicate active fully qualified C# type definitions, excluding normal migration partial classes. The more significant duplication is behavioral/configurational rather than duplicate class names.

---

# 13. Missing references or files visible from the source

## 13.1 Confirmed stale/missing paths

Cashier project exclusions reference absent files:

- `ViewModels/IssueVoucherModalViewModel.cs`
- `ViewModels/RedeemVoucherModalViewModel.cs`

Because these are explicit `Compile Remove` entries, their absence does not itself cause a missing-source compile error. It indicates abandoned/renamed implementation remnants.

## 13.2 Functionality referred to but absent/inactive

- No active `ISyncService` or cloud-sync implementation.
- No licence generator/private-key project.
- No real public licence verification key.
- No test project.
- No central logging implementation.
- No application configuration file for database/printer/terminal settings.
- No active specialised cash-drawer, barcode-hardware, or printer classes in their declared Hardware folders.
- No complete sales VAT/tax-invoice model.
- No complete lock/unlock workflow.
- No complete return-invoice workflow in the visible dialog.

## 13.3 XAML/source references

No active XAML class was found without code-behind, and no basic named event handler was missing in static scanning. Missing bindings/commands can still exist; the Dashboard command is one confirmed example.

---

# 14. Architectural risks

## Critical

1. **Schema divergence and data-loss risk:** Mixing `EnsureCreated()` with stale migrations makes database upgrades unpredictable.
2. **Tax correctness risk:** Sales VAT is absent; PO/GRN tax master integration is inconsistent.
3. **Authentication risk:** Hard-coded super-admin bypass.
4. **Licensing risk:** Placeholder cryptographic key and absent cashier enforcement.
5. **Startup correctness risk:** BackOffice double-login architecture.

## High

6. **No automated transaction tests:** Checkout updates sales, stock, vouchers, discounts, claims, and inventory in one operation without a visible test suite.
7. **Configuration risk:** Hard-coded terminal/printer/store values can cause incorrect posting or output.
8. **Incomplete operational controls:** Lock screen, Z report, returns, and some payment/report paths are not complete.
9. **Service-locator coupling:** View and ViewModel code can resolve arbitrary services, obscuring dependencies.
10. **UI-thread and async risk:** Synchronous blocking and fire-and-forget calls can freeze UI or hide errors.

## Medium

11. **Repository concrete coupling:** Limited ability to mock or substitute persistence in tests.
12. **Large centralized DbContext:** Thousands of lines of mapping make schema changes difficult to review.
13. **String-based statuses and types:** Many transaction/payment/status values are raw strings, increasing typo and invalid-state risk.
14. **Legacy compatibility accumulation:** Aliases, old comments, dormant code, and overlapping implementations increase maintenance cost.
15. **Mixed MVVM boundaries:** Business/UI responsibilities are not consistently separated.
16. **No central error/logging strategy:** Many exceptions are surfaced through `MessageBox` or caught locally; production diagnosis will be difficult.

---

# 15. Files or information that could not be inspected

## Fully inaccessible or absent from the ZIP

- The actual SQLite database used by the installed application.
- Existing customer/store data.
- Applied migration history in the live database.
- Runtime logs or crash reports.
- NuGet package contents and generated build assets.
- CommunityToolkit generated source.
- Visual Studio build output and compiler diagnostics.
- Windows printer queue/device configuration.
- Barcode scanners, cash drawer, customer display, and other attached hardware.
- Real licence files, private key, or licence-generator source.
- Cloud service/API definitions.
- Business/legal VAT requirements for the intended jurisdiction and store.
- User acceptance rules and expected screen workflows.

## Static inspection limitations

- The solution could not be compiled because the inspection environment lacks .NET/MSBuild.
- WPF bindings, resources, converters, command generation, and design-time behavior were not runtime-tested.
- EF SQL generation and database upgrade behavior were not executed.
- Printing byte streams were not sent to real hardware.
- Concurrency behavior for invoice sequences, multiple cashier terminals, and simultaneous stock changes was not tested.

---

# 16. Prioritized recovery plan

This is a recovery sequence, not a request to implement changes immediately.

## Phase 0 — Protect the current state

1. Preserve this ZIP and the current live database as immutable backups.
2. Record Visual Studio version, installed .NET SDKs, Windows version, NuGet sources, and the exact executable/database currently in use.
3. Create a clean recovery branch from the current source before any edits.
4. Do not delete legacy fields, migrations, or compatibility aliases yet.

## Phase 1 — Establish a reproducible build baseline

1. Restore and build all four projects on a Windows machine with the intended .NET 8 SDK.
2. Capture the complete restore/build error list without applying broad generated fixes.
3. Resolve package-version compatibility first, especially EF Tools/runtime alignment and any `NU1701` dependency.
4. Enable DI container validation in a diagnostic branch or test harness.
5. Run each executable far enough to confirm startup and capture runtime exceptions.

**Exit criterion:** Clean deterministic build and documented startup result.

## Phase 2 — Reconcile the database before feature changes

1. Back up the real `pos_local.db`.
2. Inspect `__EFMigrationsHistory`, tables, columns, indexes, constraints, and data.
3. Compare the live schema against:
   - current `AppDbContext`;
   - current migration snapshot;
   - each migration;
   - a clean database produced from the current model.
4. Decide one controlled schema strategy:
   - repair/continue migrations from a validated baseline; or
   - create a new baseline migration and a deliberate data-upgrade script.
5. Replace accidental `EnsureCreated()` production behavior with an explicit migration/startup policy only after the schema is reconciled.
6. Test upgrades on copied production data, never the only live database.

**Exit criterion:** One authoritative schema and a repeatable upgrade path.

## Phase 3 — Close critical security and startup defects

1. Remove or securely gate the hard-coded super-admin bypass.
2. Define initial-admin provisioning/reset behavior.
3. Resolve BackOffice to one login flow and one post-login destination.
4. Repair Dashboard and orphaned navigation routes.
5. Decide whether licensing is required now; if yes, install the real public key and enforce it in Cashier startup with defined failure/read-only behavior.
6. Add structured startup/error logging.

**Exit criterion:** Authentication, licence policy, and navigation are deterministic and auditable.

## Phase 4 — Define and implement authoritative VAT architecture

1. Confirm legal/business VAT requirements, rounding rules, invoice format, inclusive/exclusive behavior, exemptions, returns, discounts, free items, and vouchers.
2. Make `TaxRate` the authoritative master rather than parsing digits from tax-code strings.
3. Define item-to-tax relationship and transaction-time tax snapshots.
4. Add tax snapshots and totals to sales header/line architecture.
5. Integrate tax into price calculation, discounts, returns, receipts, reports, and customer/supplier documents.
6. Decide the role of `GlobalVatRate`; do not leave competing sources of truth.
7. Create calculation tests for every tax scenario before changing UI behavior.

**Exit criterion:** PO, GRN, sales, returns, receipts, and reports reconcile to the same tax rules.

## Phase 5 — Stabilize sales and financial correctness

1. Add integration tests for checkout transaction rollback and success.
2. Test invoice-sequence concurrency across multiple terminals.
3. Test stock/batch deduction, expiry, overselling, and simultaneous sales.
4. Test split payments, cash change, card/cheque, voucher sale/redemption, free issues, discount approvals, and supplier claims.
5. Test voids, returns, suspended carts, shift close, X/Z reports, and audit records.
6. Complete return, lock, Z-report, and missing payment workflows before production use.

**Exit criterion:** Transactional and accounting invariants are covered by automated tests.

## Phase 6 — Normalize configuration and hardware

1. Load terminal identity from registered terminal/settings instead of hard-coded `01`.
2. Load printer/store/receipt values from settings.
3. Choose one receipt-printer abstraction and one ownership boundary.
4. Implement/test cash drawer and other required hardware through explicit interfaces.
5. Define offline/failure behavior for printers and devices.

## Phase 7 — Gradual architecture cleanup

1. Introduce interfaces only where they support testing or replacement; avoid a wholesale rewrite.
2. Replace global service-locator calls incrementally with constructor injection or explicit dialog/navigation services.
3. Standardize ViewModel-first or View-first navigation rather than mixing both arbitrarily.
4. Move business logic out of code-behind only when covered by tests.
5. Replace raw status/type strings with validated value objects/enums where migration-safe.
6. Split `AppDbContext` mapping into entity configurations without changing the model behavior.
7. Remove dead files, duplicate registrations, aliases, and legacy code only after usage and schema checks.

## Phase 8 — Complete secondary modules and release controls

1. Finish exports, reports, customer/supplier payments, cloud sync, and backup/restore validation.
2. Add automated backup verification and restore drills.
3. Add CI build/test pipeline.
4. Add release versioning, migration packaging, configuration deployment, and rollback instructions.
5. Perform UAT using copied realistic data and real POS hardware.

---

# Final conclusion

The source is recoverable, but it should not be repaired by continuing to generate isolated replacement files. The central priority is to establish a trusted build and database baseline, then correct security/startup and tax architecture before extending features.

The existing sales repository and broad domain coverage provide a useful foundation. The main danger is that multiple generations of code and schema are currently coexisting without one authoritative migration, navigation, tax, security, or configuration strategy.
