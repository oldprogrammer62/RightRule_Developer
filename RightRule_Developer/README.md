# RightRule Developer

RightRule Developer is a Windows desktop tool for creating simulated data sessions, registering rules, and applying rules against session data. It is built with WPF and uses the RightRule NuGet package.

## Requirements

- Windows
- .NET 10 SDK (or Visual Studio with the .NET 10 desktop development workload)

The project references the RightRule package from the local `Packages` folder and uses nuget.org for its other package dependencies.

## Build and run

Open `RightRule_Developer.slnx` in Visual Studio, or run these commands from the repository root:

```powershell
dotnet restore .\RightRule_Developer\RightRule_Developer.csproj
dotnet run --project .\RightRule_Developer\RightRule_Developer.csproj
```

To build without launching the application:

```powershell
dotnet build .\RightRule_Developer.slnx
```

## Getting started

The application starts without an active RightRule session. Use **File → New RightRule session** to enter a session name and description, review the current RightRule options, and create the session. Use **File → Load RightRule session** to switch to a saved session.

Create datasets and their initial data items from **Edit → Create dataset and data items**. Add and edit individual items through the data list context menu or by double-clicking a row. Create rules and default rules from the **Edit** menu or the rules list context menu. Select a context and dataset in the second ribbon row, then choose a rule application method and click **Apply rules**. Application results, including `StatementApplied`, appear in the Activity and results panel.

Session data is stored as JSON at:

```text
%LOCALAPPDATA%\RightRule Developer\Sessions\<session name>\session.json
```

Use **File → Save session** to write the current data and rules. The application logs the saved file path in Activity and results. Closing the app prompts to save when the active session has unsaved changes.

## RightRule options and custom operators

Open **File → RightRule options** to configure data checks, diagnostics, default operators, and custom operators. These settings are global to the application and are saved at:

```text
%LOCALAPPDATA%\RightRule Developer\RightRuleOptions.json
```

Custom operators are enabled by default. The default configuration path is `Operators/CustomOperators/customoperatorconfig.json` under the application output directory. The project copies the repository's `CustomOperators` files to that output location during build and publish. Options are applied when sessions are created or loaded.

Use **Help → Loaded operators and aliases** to inspect the operators available in the active session, and **Help → About RightRule Developer** to view library versions.

## Export

Use **Export → Export rules to file** or **Export → Export data to file**. Each export can produce one JSON file containing all rules or datasets, or separate files for each context or dataset. The `ExportSamples` directory contains example rule and data JSON documents.
