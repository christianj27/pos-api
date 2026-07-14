# Pos.Test — Test Suite 🧪

Unit and integration-style tests for the `Pos.Api` project using xUnit, Moq, and EF Core InMemory. This project validates business logic in services and controller behaviors.

## Stack
- xUnit (test framework)
- Moq (mocking interfaces/services)
- EF Core InMemory (lightweight data provider for tests)
- Microsoft.NET.Test.Sdk + coverlet.collector (VS Test + coverage)

## Quick Start
From the solution root or this project folder:
```bash
# restore & build
dotnet restore
dotnet build

# run all tests
dotnet test Project/Backend/Pos/Pos.Test/Pos.Test.csproj

# collect coverage (XPlat)
dotnet test Project/Backend/Pos/Pos.Test/Pos.Test.csproj --collect:"XPlat Code Coverage"
```
Results are written under `TestResults/` (per run). Many IDEs can visualize the `coverage.cobertura.xml` emitted by Coverlet.

## Project Layout
- Controllers/ — controller-focused tests (authorization, results, status codes)
- Services/ — business logic tests (happy paths, edge cases, errors)
- Helpers/ — test utilities/builders

## Patterns & Tips
- Prefer service-level tests for complex rules; keep controller tests thin.
- Use EF Core InMemory for repositories/DbContext-based services when behavior depends on query logic.
- Mock external dependencies via Moq for deterministic tests.
- Name tests by behavior (Arrange/Act/Assert or Given/When/Then).

## Example EF InMemory Setup (snippet)
```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;
using var db = new AppDbContext(options);
```

## CI Friendly ✅
`dotnet test` exits non‑zero on failures, suitable for CI pipelines. Add `--logger trx;LogFileName=test-results.trx` to export test logs.

Happy testing! ✨