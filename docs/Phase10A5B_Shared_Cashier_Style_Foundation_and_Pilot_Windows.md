# Phase 10A.5B — Shared Cashier Style Foundation and Pilot Windows

## Purpose

Establish a lightweight, consistent classic desktop style for the Cashier application without changing sales, payment, stock, authentication, shift, VAT, or database behaviour.

## Approved visual direction

- compact professional retail desktop interface;
- native WPF controls and normal Windows dialog chrome;
- square one-pixel borders and restrained colours;
- Segoe UI typography;
- blue for normal primary actions;
- green for successful completion;
- amber for protected or exceptional financial actions;
- red only for destructive actions;
- neutral grey for secondary actions;
- no animation, blur, gradients, third-party themes, or image-heavy controls.

## Shared resources added

- `POS.Cashier.UI/Resources/CashierControls.xaml`
- `POS.Cashier.UI/Resources/CashierDialogs.xaml`

The existing `Resources/Colors.xaml` remains the authoritative colour dictionary and now includes the additional classic Cashier palette.

## Pilot windows updated

### Cashier Login

- normal Windows title bar;
- compact dialog header and footer;
- shared input, label, button, status, and error styles;
- Enter remains the login action;
- existing authentication and shift ownership rules are unchanged.

### Item Search / Price Look-Up

- normal resizable Windows dialog;
- usable minimum dimensions for smaller screens;
- compact search toolbar;
- resizable Master Item, Sellable Variant, and GRN Batch areas;
- shared virtualized DataGrid style;
- readable light row selection;
- explicit Add Item and Add Batch actions;
- existing row-tap, Enter, Escape, item, variant, and batch behaviour is preserved.

### Cash Payment

- consistent shared dialog style;
- clearer financial hierarchy;
- full touch numpad remains visible rather than being clipped;
- shared action and amount-entry styles;
- Enter and Escape behaviour is unchanged;
- cash calculation and payment commands are unchanged.

### Tender Numpad

- shared classic button styles;
- no gloss effect or animation;
- existing dependency properties and input behaviour are unchanged.

## Performance safeguards

- native WPF only;
- no third-party UI packages;
- no animation or effects;
- no new database calls;
- DataGrid row and column virtualization enabled;
- recycling virtualization enabled;
- existing ViewModels, commands, repositories, and calculations retained.

## Explicit exclusions

This phase does not redesign the main Sales terminal or the remaining Cashier dialogs. It creates and validates the shared foundation and the three approved pilot workflows first.
