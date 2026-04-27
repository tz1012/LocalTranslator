# Local Translator

Local Translator is a Windows desktop translator that uses a local LM Studio model through the OpenAI-compatible API.

## Requirements

- Windows x64
- LM Studio running locally
- A model loaded in LM Studio
- LM Studio local server enabled at `http://localhost:1234/v1`

## Features

- Global selected-text translation shortcut
- Floating translation window
- `Ctrl+Enter` insertion back into the original app
- Searchable language picker
- Clipboard text picker
- Editable translation instructions
- Configurable LM Studio settings
- GitHub Releases based update checks

## Default Shortcuts

- Translate selected text: `Ctrl+C,C`
- Insert translation: `Ctrl+Enter`

## Build

```powershell
dotnet publish .\LocalTranslatorApp\LocalTranslatorApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\dist\LocalTranslator-v0.6-win-x64
```

## Installer

Install Inno Setup 6, then run:

```powershell
iscc .\installer\LocalTranslator-v0.6.iss
```
