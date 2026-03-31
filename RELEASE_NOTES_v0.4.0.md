# TCode Launchpad v0.4.0

## Highlights

- Rich metadata browsing: business object and module data are now fully integrated into the launcher UI and search flow.
- Prefix suggestion search: typing `bo:` or `module:` shows navigable suggestion lists before applying the filter.
- Data format modernization: transaction metadata now supports structured module and business object objects loaded from remote JSON.

## New Features

### Module and business object search flows
- Typing `bo:` shows available business objects, filtered incrementally by code prefix.
- Typing `module:` shows available modules, filtered incrementally by module code or name prefix.
- Pressing `Enter` on a suggestion converts it into an exact prefix filter (`bo:CODE` or `module:CODE`) and shows matching transactions.
- Detail facets for module and business object are keyboard-selectable and can apply related-transaction filters directly.

### Structured metadata support
- `module` is now handled as an object with `code` and `name` instead of a flat string.
- `business_object` parsing is now robust against object/null JSON values.
- Result rendering de-duplicates module labels cleanly in both list rows and detail views.

### Data refresh improvements
- Forced refresh now bypasses HTTP caches and appends a cache-busting timestamp.
- After a forced refresh attempt, the app always reloads the in-memory dataset from the local cache file so the UI reflects the latest available data.

### Data normalization
- Transaction codes are normalized to uppercase.
- Metadata is loaded from the normalized remote JSON schema.
- Release packaging no longer requires bundling `data.json`; the app retrieves it at runtime.

## Validation

- Build: `dotnet build TCodeLaunchpad.sln -c Release`
- Tests: `dotnet test TCodeLaunchpad.sln`
- Tests passed: 3/3

## Release Assets

Upload these files to GitHub Release `v0.4.0`:

- `TCodeLaunchpad-v0.4.0-win-x64.zip` (self-contained executable bundle without `data.json`)
- Optional: this markdown file content as release description