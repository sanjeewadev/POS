# License Management UI Redesign

This patch changes only the License Management XAML presentation.

It keeps the existing ViewModel, commands, license verification, import logic,
machine code, store/terminal status, expiry rules, and database behavior unchanged.

## Visual changes

- Uses the same classic resource colors as Item Master, GRN, Category, and Subcategory.
- Uses compact 5-pixel page spacing and classic group boxes.
- Uses numbered sections and compact read-only fields.
- Shows operating status in one clear row.
- Shows Store License and Terminal License side by side.
- Keeps machine/request information in one compact section.
- Removes the duplicated Refresh and Import action area.
- Keeps one fixed action bar at the bottom.

## Apply

Close BackOffice and Visual Studio, then extract this ZIP into:

`C:\Users\Sanjeewa\Dev\MyProjects\POS`

Choose overwrite when asked.

## Build

```powershell
cd C:\Users\Sanjeewa\Dev\MyProjects\POS

dotnet build .\POS.sln -c Debug
dotnet build .\POS.sln -c Release
```

Both builds must complete with `0 Error(s)`.

## Test

Open BackOffice and go to Admin > License Management.
Verify:

- Store and terminal information still load.
- Refresh works.
- Copy Machine Code works.
- Copy Request Details works.
- Import License still opens the `.poslic` file picker.
- No licensing rules or status values have changed.
