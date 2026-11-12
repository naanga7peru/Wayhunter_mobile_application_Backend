# Wayhunter Backend — runnable starter (not feature-complete)
- Target .NET 10.0 LTS SDK.
- Source: Api / Application / Domain / Infrastructure plus xUnit tests.
- The only HTTP route is `GET /health`, returning a public liveness status.
- No database connection, EF migrations, auth, Coins, Hunter approval or Game winner logic is implemented.
- `docs/` and existing `.gitkeep` placeholders are preserved. M1–M7 add real code under their owner modules.

```powershell
dotnet build
dotnet test
dotnet run --project src/Wayhunter.Api --urls http://localhost:5055
# In another terminal: curl.exe http://localhost:5055/health
```
Use `Install-Backend-Starter.ps1` to merge this starter without overwriting existing files and generate the solution.
