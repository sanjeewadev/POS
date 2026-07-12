# Phase 7E1 Patch Manifest

## Added

- `POS.Core/Services/Tax/SalesTaxService.cs`
- `docs/Phase7E1_Shared_Sales_VAT_Engine_Foundation.md`

## Modified

- `POS.Core/Repositories/SalesRepository.cs`
- `POS.Core.CalculationTests/Program.cs`

## Database

No migration.

## Runtime scope

- Existing Stock Item sales retain batch and inventory controls.
- Repository-level Service sale persistence is enabled.
- Cashier UI does not expose Services until Phase 7E2.
- Gift-voucher and free-issue tax amounts remain `LegacyUnknown`.
