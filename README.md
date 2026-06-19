# CEDX Asset Manager

Windows-native IT asset inventory console for local `.txt` scan files.

## Build

```powershell
dotnet build .\Cedx.sln
```

## Run

```powershell
dotnet run --project .\src\Cedx.App\Cedx.App.csproj
```

The app looks for an `assets` folder first. If `assets` does not exist, it falls back to the current `Database` folder.

## Parser Smoke Check

```powershell
dotnet run --project .\tools\Cedx.ParserSmoke\Cedx.ParserSmoke.csproj -- .\Database
```

The smoke check loads local asset `.txt` files and reports parsed RAM, C: free space, stored credential sections, and WinRM command counts.

## Native App Status

The first WPF slice includes:

- dense inventory table,
- global search,
- OS/manufacturer/status filters,
- low storage, AnyDesk, BitLocker Off, and stored credential filters,
- selected asset details panel,
- CSV export,
- copy commands,
- AnyDesk URI launch.

The previous Python/Streamlit files are intentionally preserved until the native app fully replaces them.
