# RightRule Developer

A WPF desktop tool for building and evaluating RightRule sessions.

## Run locally

Open `RightRule_Developer.slnx` in Visual Studio, or run from this directory:

```powershell
dotnet run --project .\RightRule_Developer\RightRule_Developer.csproj
```

The project uses the RightRule package in `..\Packages` and restores its NuGet dependencies from nuget.org. Session files are stored under `%LOCALAPPDATA%\RightRule Developer`.

Use **Export → Export rules to file** or **Export → Export data to file** to create JSON files in the formats shown in `ExportSamples`. Each export can be written as one combined file or as separate files by context or dataset.

## Session files

Each session is saved as `Sessions\<session name>\session.json`.
