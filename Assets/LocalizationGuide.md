# Adding a language

The runtime language list is data-driven. Adding a language must not require a new C# enum value or a new language button.

1. Add one entry to `Resources/Localization/languages.json`.
2. Add one or more translation JSON files using the existing `{ "entries": [...] }` format.
3. List those files in the entry's `resourcePaths` without the `Resources/` prefix or file extension.
4. If the current UI font does not contain the required glyphs, put a `Font` asset under a `Resources` folder and set `fontResourcePath`.
5. Set `cultureCode`, `uiTextScale`, `listSeparator`, and `rightToLeft` for the locale.
6. Run `Tools > PICO > Validate Localization Tables` before testing.

`fallbackCode` is followed until a translation is found. English (`en`) is the final runtime fallback, and an unresolved key is displayed as its ID.

When the manifest contains more than two languages, the option screen automatically changes the two language buttons into previous/next controls and shows the current language's native name between them.
