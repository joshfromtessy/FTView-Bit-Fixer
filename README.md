# FTView Bit Fixer

FT Alarm Fixer is a desktop tool that converts FactoryTalk View alarm XML exports into a clean, corrected Excel tag list.

## What it does
- Parses FactoryTalk View XML exports (message + trigger data).
- Normalizes alarm tags and fixes 1-based bit indices to 0-based (for bit triggers).
- Cleans descriptions by removing leading bracketed prefixes.
- Sorts tags and writes an Excel file with Tag and Description columns.

Example:
- Input tag: Hmi.Alarm[0].1
- Output tag: Hmi.Alarm[0].0

## How to use
1. Launch the app.
2. Drag and drop one or more FactoryTalk View XML exports, or click Open XML Files.
3. Choose an output location (default is Alarm_Tags.xlsx next to the first XML file).
4. (Optional) Enable Ignore blank descriptions.
5. Click Export.

## Output
- Excel file with two columns: Tag, Description.
- Rows are sorted by base tag name, array index, and bit.

## Requirements
- Windows
- .NET 10 SDK to build/run from source

## Build and run
```powershell
dotnet restore
dotnet run --project .\FT_AlarmFixer.csproj
```