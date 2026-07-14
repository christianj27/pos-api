# Pos.Api — POS Backend Service

A production-ready REST API for a point-of-sale workflow (water & gas distribution) built with ASP.NET Core and Entity Framework Core on PostgreSQL. It implements role-based auth (owner, kurir, kasir), JWT access tokens with rotating refresh tokens, rate-limited login, CORS for the web frontend, and opinionated security headers.

## Overview
- Runtime: .NET 10 (TargetFramework: `net10.0`)
- Web: ASP.NET Core Minimal Hosting
- Data: EF Core + Npgsql (PostgreSQL)
- Auth: JWT access token + HttpOnly refresh cookie
- Validation: FluentValidation (selectively used)
- Docs: Built-in OpenAPI document (dev only)

Key code entry points
- Startup & pipeline: Program.cs
- Data access: Data/AppDbContext.cs
- Seed & migrations: Data/DbSeeder.cs + Migrations/
- Endpoints: Controllers/*
- Services (business logic): Services/Implementations/* and Services/Interfaces/*
- Security headers: Middleware/SecurityHeadersMiddleware.cs

## Features
- Role policies: `OwnerOnly`, `OwnerOrKurir`, `OwnerOrKasir`, `AllStaff`
- Login rate-limiting: 10 attempts / 15 minutes per IP
- Rotating refresh tokens stored server-side with revocation
- CORS allow-list via `Cors:AllowedOrigin`
- Automatic database migration + initial seed on startup (dev-friendly)
- Comprehensive domain: users, locations, products, stock, transactions, payments, debts, container loans, assignments, dashboard, cashflow

## Prerequisites
- .NET SDK 10.x
- PostgreSQL 14+ running locally (or accessible connection string)

## Configuration
App settings templates live in appsettings.example.json. For local development either copy it to appsettings.Development.json or set environment variables.

Required settings
- ConnectionStrings:DefaultConnection — PostgreSQL connection string
- Jwt:SecretKey — minimum 32 chars random secret
- Jwt:Issuer — issuer claim value (e.g. https://localhost:7075)
- Jwt:Audience — audience claim value (e.g. pos-app)
- Cors:AllowedOrigin — comma-separated list of allowed origins (e.g. http://localhost:5173)

Environment variable equivalents
- `ConnectionStrings__DefaultConnection`
- `Jwt__SecretKey`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Cors__AllowedOrigin`

## Database & Seeding
- Tools: local manifest at dotnet-tools.json (includes `dotnet-ef` 10.x)
- Apply migrations and create database:
  ```bash
  dotnet tool restore
  dotnet ef database update
  ```
- On first run, `DbSeeder` ensures:
  - Default owner user: username `owner`, password `owner1234`
  - Default warehouse location: "Gudang Utama"

## Running Locally
Default development URLs are defined in Properties/launchSettings.json
- HTTP: http://localhost:5094
- HTTPS: https://localhost:7075

Run the API from the Pos.Api directory
```bash
dotnet restore
dotnet build
dotnet run
```

OpenAPI document (development only)
- Enabled via `builder.Services.AddOpenApi()` and `app.MapOpenApi()`
- Served at: `/openapi/v1.json` (depending on OpenAPI defaults)

## Authentication
- `POST /api/auth/login` returns an access token (JWT, ~60 minutes) and sets a `refreshToken` HttpOnly, Secure, SameSite=Strict cookie valid for ~7 days.
- `POST /api/auth/refresh` rotates the refresh token and issues a new access token.
- `POST /api/auth/logout` revokes the current refresh token and clears the cookie.
- Add header to protected endpoints:
  ```http
  Authorization: Bearer <access_token>
  ```
Roles and policies
- `owner` — full access
- `kasir`, `kurir` — restricted according to policy attributes on controllers/actions

## CORS & Rate Limiting
- CORS: policy name `FrontendPolicy`; origins from `Cors:AllowedOrigin`. Allows credentials.
- Rate limiting: `LoginPolicy` — max 10 attempts per 15 minutes per IP. Exceeding returns HTTP 429.

## Health Check
- `GET /api/health` → `{ "status": "healthy", "timestamp": "..." }`

## Selected Endpoints
- Auth: `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`
- Users (owner-only for write): `/api/users`
- Products (read: all staff; write: owner): `/api/products`
- Transactions (all staff): `/api/transactions`
- Profile (all roles): `/api/profile`
- Plus: locations, customers, stock, payments, debts, container loans, assignments, dashboard, cashflow

For full field-level request/response contracts, see Document/API_CONTRACT.md.

## Conventions
- JSON property names: snake_case in API responses
- Timezone handling: dates in queries as `YYYY-MM-DD` (interpreted as Asia/Jakarta, UTC+7); timestamps in responses are ISO 8601 UTC
- Money values use 2-decimal precision

## Testing
From the solution root or the Backend project folder
```bash
dotnet test Project/Backend/Pos/Pos.Test/Pos.Test.csproj
```

## Deployment Notes
- Set all `Jwt:*` and `ConnectionStrings:*` via environment variables or secure configuration providers
- Configure `Cors:AllowedOrigin` to your production frontend origin(s)
- In non-development, HTTPS redirection is enabled by default
- Run migrations as a separate step (`dotnet ef database update`) or ensure the app has permissions if relying on startup migration

## Troubleshooting
- 401/403 on protected routes: verify `Authorization` header and that the user role satisfies the policy
- 429 on login: you’ve hit rate limit; wait or change IP
- CORS errors: confirm the exact origin (scheme, host, port) is listed in `Cors:AllowedOrigin`
- Database errors on startup: verify PostgreSQL is reachable and credentials are valid

---
