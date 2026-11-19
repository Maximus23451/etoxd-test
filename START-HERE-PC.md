# ETO portal — continue development on your PC

This ZIP contains the latest React + ASP.NET Core + Microsoft SQL Server source from the cloud workspace, including the calendar, multiple age-group memberships, delegated transfers, system manager, audit log, error/loading test page, themes and continuous reviews. It includes changes not yet pushed to GitHub.

It does NOT contain the cloud database, credentials, session keys, installed dependencies or build outputs. The backend creates a new database schema and development demo users on first start. Your existing cloud database is not being migrated by this archive.

## 1. Install the prerequisites

- Node.js 24 or newer: https://nodejs.org/
- .NET SDK **10.0.404**, as pinned by `global.json`: https://dotnet.microsoft.com/download/dotnet/10.0
- Microsoft SQL Server. On Windows, SQL Server Express with Windows authentication is a simple local option. SQL Server Management Studio is optional.
- An editor, such as VS Code or Visual Studio with .NET 10 support.

Extract the whole `etoxd` folder somewhere writable, such as your Documents folder. Open a terminal in that folder. Keep `public/assets` with the project; it contains the fonts and club crest.

The instructions below use Windows PowerShell. The original `README.md` has a fuller feature reference in Hungarian. Some existing npm helper scripts were written for the cloud machine, so use these direct commands on a PC rather than `npm run db:start`, `npm run build:api` or `npm run dev`.

## 2. Install and build

From the extracted `etoxd` folder:

```powershell
npm ci
dotnet restore api/Eto.Api.csproj --locked-mode
dotnet build api/Eto.Api.csproj --no-restore
```

The .NET SDK must match `global.json`. If a newer feature-band SDK is your only installation, either install the pinned SDK or deliberately update `global.json` as part of your own development work.

## 3. Configure your own local SQL Server

Check your instance name in SQL Server Management Studio. The common Express instance name is `localhost\SQLEXPRESS`. The Windows account that starts the API needs permission to create the `EtoPortal` database, or an administrator must create the empty database and grant appropriate access first.

From the project root in PowerShell:

```powershell
New-Item -ItemType Directory -Force .runtime | Out-Null
@{
    ConnectionStrings = @{
        Portal = 'Server=localhost\SQLEXPRESS;Database=EtoPortal;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=30'
    }
} | ConvertTo-Json -Depth 3 | Set-Content .runtime/config.json -Encoding utf8
```

Change the server name to match your installation. This example uses your Windows account, so no database password is embedded. `TrustServerCertificate=True` is for your local development SQL Server's self-signed certificate; a deployed server should have a verified certificate.

For an existing SQL login or remote SQL Server, set your own connection string in that local file instead. Do not commit it. `.runtime/` is already ignored by Git. The API also accepts the `ConnectionStrings__Portal` environment variable, which overrides the file.

On Linux or macOS, use a reachable SQL Server instance or an x86-64 SQL Server Docker host and SQL authentication instead of the Windows-only Integrated Security connection above. Create the same `.runtime/config.json` structure with your own connection details. The frontend and backend build commands are otherwise the same; use your shell's `export` syntax for the environment variables below.

## 4. Start the API

In a terminal at the project root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:3001'
dotnet run --project api/Eto.Api.csproj --no-launch-profile --no-build
```

Wait for the server to say it is listening. In a browser, `http://127.0.0.1:3001/api/health` should return `status: ok`.

If you change C# files, stop the API, rebuild it, and restart. Alternatively use `dotnet watch --project api/Eto.Api.csproj run --no-launch-profile` with the same environment variables.

## 5. Start React

In a second terminal at the project root:

```powershell
npx vite --host 127.0.0.1
```

Open **http://127.0.0.1:5173**. Vite forwards `/api` requests to ASP.NET on port 3001. React changes reload automatically.

Development demo password: **eto1904**.

| Account | TalentX ID |
|---|---|
| System manager | TX-0001 |
| Admin | TX-9001 |
| U17 coach | TX-2001 |
| U17 player | TX-1001 |
| U15 player Józsi | TX-1501 |

The manager's **Állapotteszt** page exercises loading and 401/403/404/500 states. Player transfers are under **Játékosok & korosztályok**. Multi-group permissions are configured under **Szerepkörök**.

Development mode seeds demo users only into an empty database. It does not reset an existing database automatically. Do not expose these demo credentials on a public deployment.

## 6. Build React for serving through ASP.NET

```powershell
npm run build
```

Restart the API after the build. It can now serve both the frontend and `/api` from **http://127.0.0.1:3001** without Vite. This is a local development setup, not a completed Coolify deployment configuration.

## 7. Optional automated tests

The source includes SQL-backed API integration tests and Playwright browser tests. They create separate `EtoPortal_Test_...` databases and remove only those databases after completion. Use a local development SQL account with database creation/deletion permission, not a production connection.

The test harness was last verified in the Linux cloud environment. For Windows, set `DOTNET_ROOT` to your actual SDK installation directory, ensure `python3` resolves to Python 3, and point `CHROMIUM_PATH` to an installed Chromium/Chrome executable. For example, adjust these paths if necessary:

```powershell
$env:DOTNET_ROOT = 'C:\Program Files\dotnet'
$env:CHROMIUM_PATH = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
npm test
npm run test:browser
```

Alternatively install Playwright's Chromium with `npx playwright install chromium` and set `CHROMIUM_PATH` to its executable. The tests use the built React `dist` directory and the Debug API build, so build both before running them.

## Project map

- `src/`: React UI, API client, calendar, memberships, accounts, activity log, themes.
- `api/`: ASP.NET Core, EF Core SQL Server model, authentication, imports, seed data.
- `public/assets/`: original crest and fonts.
- `tests/`: API/browser tests and an XLSX fixture generator.
- `scripts/`: cloud setup helpers; inspect paths before reusing them on another machine.
- `package-lock.json` and `api/packages.lock.json`: exact dependency locks.

No `.git` history is included. You can initialize a fresh local repository with `git init` and attach your GitHub remote yourself. Build caches and `.runtime/` should remain untracked.

The last cloud validation before packaging passed 10 SQL-backed API tests and the browser suite. Packaging verifies archive integrity, not a new Windows installation. Database migrations, backups, TLS/proxy setup and production account provisioning remain deployment work.
