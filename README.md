# BikriBoost

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
