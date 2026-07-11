# Phase 7C1 Compile Hotfix

This hotfix corrects the named constructor arguments used when creating `TaxCategorySeed` values in `TaxRateRepository.cs`.

C# named arguments are case-sensitive. The record parameters are `IsRateBased` and `DisplayOrder`, but the original patch used `isRateBased` and `displayOrder`.

No database, migration, UI, or runtime behavior is changed by this hotfix.
