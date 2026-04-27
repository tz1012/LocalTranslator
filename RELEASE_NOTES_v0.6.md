# Local Translator v0.6

Local Translator is a Windows translator app that uses an LM Studio local model through the OpenAI-compatible API.

## What's New

- Added GitHub Releases based update checks.
- Added tray menu item for manual update checks.
- Added update settings for repository and installer asset name matching.
- Added update notifications when a newer release is available.
- The app can download the installer from GitHub Releases and launch it.

## Included From v0.5

- Translate selected text with a global shortcut.
- DeepL-like floating translation window.
- Insert translated text back into the original app with `Ctrl+Enter`.
- Main translation workspace.
- Automatic translation while typing in the source panel.
- Searchable language picker with broad language options.
- Clipboard text picker.
- Editable translation instructions.
- Configurable LM Studio endpoint, model, temperature, and max tokens.
- Configurable shortcuts.
- App icon and installer packaging.

## Requirements

- Windows x64
- LM Studio running locally
- A model loaded in LM Studio
- LM Studio local server enabled at `http://localhost:1234/v1`

## Default Shortcuts

- Translate selected text: `Ctrl+C,C`
- Insert translation: `Ctrl+Enter`

## Notes

- Updates are checked through GitHub Releases from `tz1012/LocalTranslator`.
- Translation quality and speed depend on the loaded LM Studio model.
- If LM Studio reports that the context size was exceeded, select a shorter passage or increase the model context length in LM Studio.
- This build is unsigned, so Windows SmartScreen may show a warning on first launch.
