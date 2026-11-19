# BikriBoost

## Open and test in VS Code (Windows)

This project contains ASM Asif Islam's features 18–20 and the shared team baseline. Open **BikriBoost.code-workspace**
with VS Code, or use **File > Open Folder** and select this folder.
See [LOCAL_SETUP.md](docs/LOCAL_SETUP.md) for the folder structure and troubleshooting.

1. Accept the recommended **C#** extension if prompted.
2. Press **F5** and choose **BikriBoost: local demo** to build, debug, and open the app.
   Alternatively, select **Terminal > Run Task > BikriBoost: run demo**.
3. Open <http://localhost:5071> and sign in with `owner@demo.bikriboost.test`
   and password `Demo@12345`.
4. Select **Terminal > Run Task > BikriBoost: test** to run all automated tests.

The VS Code tasks find the .NET 10 SDK already installed on this machine, even when
`dotnet` is not on your system PATH. The local demo creates its SQLite database
automatically; XAMPP and API keys are not required. Stop a task with **Ctrl+C**
in its terminal, or stop an F5 session with **Shift+F5** before starting another.

You can also run these commands in a PowerShell terminal from this folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Demo.ps1
# Run the tests in another terminal:
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-Project.ps1 -Action Test
```

## Implemented marketing features

- **Section 1:** 18 — post approvals and image annotations; 19 — marketing calendar and stock checks; 20 — versioned assets and brand validation.

Run `./Start-Demo.ps1` from PowerShell, then open http://localhost:5071. Development uses a local SQLite demo database and sample records, so no API key or running XAMPP service is needed. The first package restore needs internet access. The script finds either an installed .NET 10 SDK or the local SDK installed for this workspace.

Sign in as `owner@demo.bikriboost.test` or `marketer@demo.bikriboost.test`, using `Demo@12345`. Only Owner can approve posts and change brand settings. Publishing is explicitly simulated: nothing is sent to social media.

Run `dotnet test` to verify the features. See [the feature walkthrough](docs/FEATURE_WALKTHROUGH.md) for demo steps, calculation rules and integration points.

For the original MariaDB setup below, set `Database__Provider=MySql` before running the app. Keep the existing connection string or supply `ConnectionStrings__Default` through environment variables. Apply migrations with `dotnet ef database update --project BikriBoost.Web -- --environment Production`. SQLite is only a Development demo option; MariaDB remains supported. Local database files and uploads under `App_Data` are ignored by Git.

Features 18-20 are integrated with the group repository's existing features 2, 4 and 5. The order and payment screens, services, and migrations are preserved. Marketing navigation links the two workspaces.

CSE470 project: ASP.NET Core MVC (.NET 10) with MariaDB (XAMPP), EF Core 9 code-first.
Order and payment control for Facebook sellers in Bangladesh.

## First-time setup

1. Install **XAMPP 8.2.12** and click **Start** next to MySQL in the XAMPP Control Panel.
2. Install the **.NET 10 SDK**: https://dotnet.microsoft.com/download/dotnet/10.0
3. Install the EF Core tool (same version for everyone):
   ```
   dotnet tool install --global dotnet-ef --version 9.0.21
   ```
4. Open a terminal in the `BikriBoost.Web` folder and create the database:
   ```
   dotnet ef database update
   ```
   This creates the `bikriboost` database with every table, plus the roles, districts, couriers and platform rules.
5. Run the app:
   ```
   dotnet run
   ```
   Open the address it prints (http://localhost:5071). On first start, `DbSeeder` adds the demo shop, its products and these logins (password `Demo@12345` for all):

   | Role | Email |
   |---|---|
   | Owner | owner@demo.bikriboost.test |
   | Moderator | moderator@demo.bikriboost.test |
   | WarehouseClerk | warehouse@demo.bikriboost.test |
   | Marketer | marketer@demo.bikriboost.test |

If you set a root password in XAMPP, don't edit `appsettings.Development.json`. Run this in `BikriBoost.Web` instead:
```
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Port=3306;Database=bikriboost;User=root;Password=YOUR_PASSWORD;"
```

## Changing the database

- Pull first, then make your entity change and run `dotnet ef migrations add <Name>`.
- Commit the migration and snapshot files, then push. Keep one migration per pull request.
- Everyone else pulls and runs `dotnet ef database update`.
- Never edit a migration that is already pushed. If two migrations clash, the second person runs `dotnet ef migrations remove`, pulls, and adds theirs again.
- Don't share the database as phpMyAdmin `.sql` exports, and never commit XAMPP's `data` folder.

## Where things are (MVC)

- **Model**: entities `BikriBoost.Web/Models/Entities/`, business rules (services) `BikriBoost.Web/Models/Services/`,
  DbContext, reference data and seeder `BikriBoost.Web/Models/Data/`, migrations `BikriBoost.Web/Models/Data/Migrations/`
- **View**: `BikriBoost.Web/Views/` (Razor pages that only display a view model)
- **Controller**: `BikriBoost.Web/Controllers/` (check the role, call one service, show a view or redirect)
- Tests: `BikriBoost.Tests/Unit/`

## Features

| # | Feature | Service (rules) | Controller | Tests |
|---|---|---|---|---|
| 2 | In-chat order parsing & quick draft | `OrderTextParser`, `OrderService` | `OrdersController` (Capture, Create) | `OrderTextParserTests` |
| 5 | Strict state-machine order lifecycle | `OrderLifecycleService` | `OrdersController` (Confirm, Dispatch, …) | `OrderLifecycleTests` |
| 4 | MFS payment & TrxID verification | `PaymentVerificationService` | `PaymentsController` | `PaymentVerificationTests` |

How each one works, a demo script and known limitations: `docs/Viva_Notes_Features_2_5_4.md`.

## Running the tests

```
dotnet test
```

The tests use an in-memory SQLite copy of the same model, so XAMPP doesn't need to be running.
