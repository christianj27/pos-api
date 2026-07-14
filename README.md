# Pos Solution 🚀

This solution bundles the backend API and its test suite:
- Pos.Api — ASP.NET Core REST API (PostgreSQL, JWT, CORS, rate limiting)
- Pos.Test — xUnit/Moq/EF InMemory tests for the API

## Prerequisites
- .NET SDK 10.x
- PostgreSQL 14+ (for the API)

## Build Everything
```bash
# from this folder (solution root)
dotnet restore

dotnet build Pos.slnx
```

## Database & Migrations
```bash
# enter API project folder
cd Pos.Api

# ensure tools and apply migrations
dotnet tool restore

dotnet ef database update
```

## Run the API 🔧
```bash
cd Pos.Api

dotnet run
```
- Dev URLs: http://localhost:5094 and https://localhost:7075 (see Properties/launchSettings.json)
- First run seeds: owner user `owner/owner1234`, location "Gudang Utama"

## Run Tests 🧪
```bash
# from solution root or Pos.Test folder

dotnet test Pos.Test/Pos.Test.csproj

# with coverage

dotnet test Pos.Test/Pos.Test.csproj --collect:"XPlat Code Coverage"
```

## Structure
```
Pos.slnx
Pos.Api/
Pos.Test/
```

## Helpful Links 🔗
- API README: [Pos.Api/readme.md](Pos.Api/README.md)
- Test README: [Pos.Test/readme.md](Pos.Test/README.md)

Happy building! ✨
