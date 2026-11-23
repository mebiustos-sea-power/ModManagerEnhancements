# Translating Mod Manager Enhancements

MME uses Sea Power's standard language dictionary. The DLL contains translation keys and formatting code; the UI text is in `package/language_en/mod_manager_enhancements.json` and `package/language_ja/mod_manager_enhancements.json`.

A separate translation mod can add or override MME text without modifying or redistributing MME's DLL.

## Create a translation mod

1. Copy the English JSON file into a normal Sea Power mod folder under `language_<code>/mod_manager_enhancements.json`. Use the code from the game's language folders, such as `fr`, `de`, or `ru`; Chinese uses `cn`.
2. Translate the values. Keep the `ModManagerEnhancements` object name, existing keys, and formatting placeholders unchanged.
3. Include the mod's own `_info.ini`. Enable both MME and the translation mod. Put the translation mod above MME in the manager when overriding a bundled language.
4. Restart Sea Power after adding, enabling, or editing the translation, so the game rebuilds its language dictionary.

For example:

```text
MyMMEFrenchTranslation/
  _info.ini
  language_fr/
    mod_manager_enhancements.json
```

A partial translation is valid:

```json
{
  "ModManagerEnhancements": {
    "SearchPlaceholder": "Rechercher des mods",
    "Sync": "Synchroniser",
    "CompatibilityConfirmation": "Version {1} : {0}"
  }
}
```

Missing translations use Sea Power's per-key English fallback. Add more translated keys from the shipped English file to complete the translation. The example's last value demonstrates argument reordering; a complete translation should retain the confirmation's explanation and warnings.

Use UTF-8 JSON. Write line breaks as `\n`, and escape quotation marks as `\"`. Do not add a `ReplacesFolders` rule for a whole language folder: ordinary language files are sufficient, and replacing the folder would hide other translations.

MME does not add entries to the game's language selector. A translation targets a language supported by Sea Power. Game-standard captions continue to use the game's own translation keys.

## Stable keys and placeholders

The English file is the full key reference. Keys are an extension contract for translation authors: do not rename them when translating. Text changes do not require a DLL rebuild.

Formatted values use .NET composite formatting:

| Key | Arguments |
| --- | --- |
| `FilteredSelection` | `{0}`: selected mod name |
| `CompatibilityConfirmation` | `{0}`: mod name; `{1}`: current game version |
| `CompatibilityRestoreConfirmation` | `{0}`: mod name |
| `CompatibilityFailure` | `{0}`: error details |
| `Summary` | `{0}`: overlapping file count |
| `Tooltip` | `{0}`: priority count; `{1}`: overridden count; `{2}`: localized click instruction |

Arguments may be reordered. To display a literal brace in a formatted value, use `{{` or `}}`. Braces inside mod names are inserted as data and need no escaping. The `FilterOverridesAction` and `ClearFilterAction` values supply the tooltip's click instruction.

Invalid JSON or invalid format placeholders are errors, not a request to substitute hardcoded text. Check the game log if a translation does not load. A key missing from both the selected language and English uses the game's missing-text error handling.

## Lifetime

MME resolves the current language handler each time it requests a string; it does not keep a handler from an old scene or subscribe to its events. Game-provided language fallback and mod priority remain owned by Sea Power.
