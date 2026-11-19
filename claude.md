# BikriBoost project guide

## Overview

BikriBoost is an e-commerce management project built with ASP.NET Core MVC,
.NET 10, and Entity Framework Core 9. The current implementation includes
marketing features 18–20: post approvals, scheduling, and asset and brand management.

Read `README.md`, `docs/LOCAL_SETUP.md`, and `docs/FEATURE_WALKTHROUGH.md`
before making changes. Features 2, 4, and 5 are present from the group repository; preserve their services, screens, tests, and migrations.

## Structure

- `BikriBoost.Web/`: web application.
- `BikriBoost.Web/Controllers/`: MVC actions and HTTP endpoints.
- `BikriBoost.Web/Models/Entities/`: database entities.
- `BikriBoost.Web/Models/Data/`: DbContext, seed data, and migrations.
- `BikriBoost.Web/Models/Services/`: business logic.
- `BikriBoost.Web/ViewModels/` and `Views/`: view data and Razor templates.
- `BikriBoost.Web/Jobs/`: background processing.
- `BikriBoost.Web/wwwroot/`: frontend assets and vendor libraries.
- `BikriBoost.Tests/Unit/` and `Integration/`: automated tests.
- `scripts/`: SDK discovery and build/test helpers.
- `.vscode/`: VS Code tasks, debugging, and local SDK settings.
- `BikriBoost.slnx`: solution containing both projects.
- `BikriBoost.code-workspace`: workspace to open in VS Code.

## Run, build, and test

Run commands from the project root in PowerShell:

```powershell
# Start the local app; keep this process running while using the browser.
powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Demo.ps1

# Build or test from a separate terminal.
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-Project.ps1 -Action Build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-Project.ps1 -Action Test
```

The default URL is `http://localhost:5071/`. Use `-Port 5072` on
`Start-Demo.ps1` if another process occupies the default port.

The scripts locate an installed .NET 10 SDK or the local installation at
`%LOCALAPPDATA%\BikriBoost\dotnet\dotnet.exe`. `global.json` selects .NET 10.
The first package restore requires internet access.

In VS Code, open `BikriBoost.code-workspace`. Use **Terminal > Run Task** for
build, test, and run tasks. **F5** starts the configured local demo debugger.

## Database and demo access

Development uses SQLite at `BikriBoost.Web/App_Data/marketing-demo.db`, with
automatic demo seeding. XAMPP and API keys are not required for this mode.
The optional MariaDB setup is documented in the README.

Demo accounts:

- Owner: `owner@demo.bikriboost.test`
- Marketer: `marketer@demo.bikriboost.test`
- Demo password: `Demo@12345`

Social publishing is simulated. Automated integration tests use isolated
in-memory SQLite databases.

## Working conventions

- Preserve existing user changes and the current MVC folder structure.
- Keep business rules in services and use the existing authorization and
  merchant isolation mechanisms.
- Do not delete the local database or uploads to resolve an issue without
  explaining the data loss and obtaining authorization.
- Do not commit generated `bin/`, `obj/`, `App_Data/`, or `artifacts/` contents.
- Keep credentials out of source code; use environment variables or user secrets
  for non-demo configuration.
- Run the relevant build and tests after application changes.
- Before starting another server, check whether port 5071 already has a listener.
- A saved codebase does not serve localhost by itself. If the browser reports
  a refused connection, check that the app process is running and review its logs.
- When leaving a demo available for the user, keep its server running and state
  how it was started. Do not stop it after verification without making that clear.
