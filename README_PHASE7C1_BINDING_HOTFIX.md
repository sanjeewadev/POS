# Phase 7C1 Tax Rate binding hotfix

Replaces only:

`POS.BackOffice.UI/Views/Pages/InventoryPages/TaxRateView.xaml`

Fix: the read-only `SelectedCategoryName` property is now bound to the read-only Category TextBox using `Mode=OneWay`, preventing the WPF runtime exception.
