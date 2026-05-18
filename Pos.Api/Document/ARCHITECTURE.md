# POS App — Architecture Plan
> MSMe Water & Gas | Last updated: May 10, 2026

---

## 1. Overview

A web-based Point of Sale system for a Micro/Small/Medium Enterprise selling **bottled water** and **cylinder gas**. The system manages stock, transactions, customers with custom pricing, and will support payment gateway integration in a future phase.

**Core users:**
- **Owner** — full access: dashboard, stock management, customer management, user management, reports, debt management
- **Kurir (Courier)** — mobile: view and process delivery assignments, delivery transactions, collect payment, view stock levels and movement history
- **Kasir (Clerk)** — mobile: counter sales at warehouse, collect payment, create delivery assignments, transfer stock, record production

**Key constraints:**
- Strictly free deployment (Phase 1)
- Ship quickly
- API-first design to support future mobile app with minimal refactoring
- **All screens must be mobile-friendly** — Owner, Kurir, and Kasir all use mobile phones as primary device

---

## 2. Tech Stack

| Layer | Technology | Hosting | Reason |
|---|---|---|---|
| Frontend | React + SCSS | Vercel (free) | Familiar, mobile-responsive, free global CDN |
| Backend | ASP.NET Core Web API (.NET 10 LTS) with Controllers | MonsterASP.NET (free) | Familiar, no cold-start, built for .NET/IIS |
| Database | PostgreSQL 16 | Neon.tech free — Singapore region | No project pausing, low latency to Indonesia (~30–50ms), free forever |
| ORM | EF Core 10 + Npgsql | — | Standard .NET + PostgreSQL stack |
| Auth | JWT (HS256) | — | Stateless, role-based, works for web and future mobile |
| Styling | SCSS (CSS modules per component) | — | No framework dependency, easy to understand and maintain |
| Charts | Chart.js v4 + react-chartjs-2 v5 | — | Interactive bar chart with click-to-detail panel; MIT-licensed, tree-shakeable (register only `CategoryScale`, `LinearScale`, `BarElement`, `Tooltip`) |
| Real-time | React polling (5–10s interval) | — | Simple, zero extra infra needed for small MSMe scale |

### Why .NET 10 over .NET 9?
.NET 10 is the current **LTS (Long Term Support)** release (November 2025 → November 2028, 3 years). .NET 9 is STS with support ending **May 2026** — it is already end-of-life. Always prefer LTS for production apps.

### Why Controllers over Minimal API?
With 7+ feature areas (auth, stock, transactions, customers, users, payments, dashboard), Controllers give a clean, organized project structure from day one. Minimal API becomes harder to navigate beyond ~40 endpoints.

### Why PostgreSQL over SQL Server?
- No free cloud SQL Server hosting outside time-limited trials (SmarterASP.NET = 60-day trial only; MonsterASP.NET free = EU datacenter only → ~200ms latency from Indonesia)
- Neon.tech PostgreSQL free tier: Singapore datacenter, never pauses, 0.5 GB storage, multi-AZ storage by default
- EF Core migrations are identical — only the provider (Npgsql) changes

### Why MonsterASP.NET for API?
- Free forever, no cold-start (IIS always-on), no UptimeRobot workaround needed
- Built specifically for .NET — Visual Studio publish support, AppPool management
- SmarterASP.NET is eliminated: 60-day trial only, not free forever

---

## 3. Deployment Architecture

```
┌─────────────────────────────────────────────┐
│  Courier (mobile browser)  Owner (browser)  │
└──────────────────┬──────────────────────────┘
                   │  HTTPS
                   ▼
┌──────────────────────────────────────────────┐
│         React SPA (SCSS, mobile-first)        │
│         Vercel — global CDN, free             │
└──────────────────┬───────────────────────────┘
                   │  HTTPS REST + JWT
                   ▼
┌──────────────────────────────────────────────┐
│      ASP.NET Core Web API (.NET 10 LTS)        │
│      MonsterASP.NET — IIS, free, always-on    │
└──────────────────┬───────────────────────────┘
                   │  EF Core + Npgsql (TCP)
                   ▼
┌──────────────────────────────────────────────┐
│   PostgreSQL 16 — Neon.tech                   │
│   Region: ap-southeast-1 (Singapore)          │
│   Free tier: 0.5 GB, never pauses             │
└──────────────────────────────────────────────┘
```

### Environment Variables (never commit to git)
**API (`appsettings.Production.json` / MonsterASP.NET env):**
```
ConnectionStrings__DefaultConnection  = postgresql://...neon.tech/posdb?sslmode=require
Jwt__SecretKey                        = <32+ char random secret>
Jwt__Issuer                           = https://your-api.monsterasp.net
Jwt__Audience                         = pos-app
Jwt__AccessTokenExpiryMinutes         = 60
Jwt__RefreshTokenExpiryDays           = 7
```

**Frontend (Vercel env):**
```
VITE_API_BASE_URL = https://your-api.monsterasp.net
```

---

## 4. Domain Model

```
Users
  id              UUID  PK
  name            VARCHAR(100)
  username        VARCHAR(50)  UNIQUE
  password_hash   VARCHAR(255)
  role            ENUM('owner', 'kurir', 'kasir')
  is_active       BOOLEAN
  created_at      TIMESTAMPTZ

Locations
  id              UUID  PK
  name            VARCHAR(100)
  type            ENUM('warehouse', 'vehicle')
  assigned_to     UUID  FK → Users  -- active user (owner or kurir); required if vehicle
  is_active       BOOLEAN
  created_at      TIMESTAMPTZ

Products
  id              UUID  PK
  name            VARCHAR(100)
  category        ENUM('simple', 'refillable')
  production_type ENUM('purchased', 'selfproduced')  -- nullable; required if refillable
  type            ENUM('air', 'gas')
  unit            VARCHAR(20)   -- e.g. "galon", "tabung", "karton"
  base_price      DECIMAL(15,2)
  is_active       BOOLEAN
  created_at      TIMESTAMPTZ

Customers
  id              UUID  PK
  name            VARCHAR(100)
  phone           VARCHAR(20)
  address         TEXT
  is_active       BOOLEAN
  created_at      TIMESTAMPTZ

CustomerPricing                     -- per-customer price override
  customer_id     UUID  FK → Customers
  product_id      UUID  FK → Products
  custom_price    DECIMAL(15,2)
  PRIMARY KEY (customer_id, product_id)

StockMovements
  id              UUID  PK
  product_id      UUID  FK → Products
  movement_type   ENUM('receive', 'transfer', 'dispatch', 'defect', 'production')
  container_status ENUM('filled', 'empty', 'na')
  quantity        INT              -- always positive
  from_location_id UUID  FK → Locations  -- null for 'receive'; null when sending empties to external vendor
  to_location_id  UUID  FK → Locations  -- null for 'dispatch', 'defect'; null when sending empties to external vendor
  purchase_cost   DECIMAL(15,2)   -- nullable; required on vendor-exchange receive movement
  note            TEXT             -- required for defect; optional otherwise; max 255
  created_by      UUID  FK → Users
  created_at      TIMESTAMPTZ

Transactions
  id              UUID  PK
  transaction_type ENUM('delivery', 'counter')
  customer_id     UUID  FK → Customers  -- nullable; null for anonymous sales
  staff_id        UUID  FK → Users
  location_id     UUID  FK → Locations  -- source location (vehicle or warehouse)
  status          ENUM('completed', 'cancelled')
  payment_method  ENUM('cash', 'transfer', 'qris')
  total_amount    DECIMAL(15,2)
  paid_amount     DECIMAL(15,2)
  debt_amount     DECIMAL(15,2)   -- computed: total_amount - paid_amount; stored for query performance
  notes           TEXT
  created_at      TIMESTAMPTZ
  completed_at    TIMESTAMPTZ

TransactionItems
  id              UUID  PK
  transaction_id  UUID  FK → Transactions
  product_id      UUID  FK → Products
  quantity        INT
  unit_price      DECIMAL(15,2)   -- snapshot at time of sale

Payments                            -- multiple payments per transaction supported
  id              UUID  PK
  transaction_id  UUID  FK → Transactions
  amount          DECIMAL(15,2)
  method          ENUM('cash', 'transfer', 'qris')
  reference_no    VARCHAR(100)     -- optional; payment gateway reference in Phase 2
  paid_at         TIMESTAMPTZ

DebtPayments                        -- standalone customer debt settlement
  id              UUID  PK
  customer_id     UUID  FK → Customers
  amount          DECIMAL(15,2)
  method          ENUM('cash', 'transfer', 'qris')
  reference_no    VARCHAR(100)
  note            TEXT
  created_by      UUID  FK → Users
  created_at      TIMESTAMPTZ

ContainerLoans                      -- containers lent to customers; negative quantity = returned
  id              UUID  PK
  transaction_id  UUID  FK → Transactions  -- nullable for standalone returns
  customer_id     UUID  FK → Customers
  product_id      UUID  FK → Products      -- refillable products only
  quantity        INT                       -- positive = lent to customer; negative = returned
  created_by      UUID  FK → Users
  created_at      TIMESTAMPTZ

RefreshTokens
  id              UUID  PK
  user_id         UUID  FK → Users
  token_hash      VARCHAR(255)   -- SHA-256 hash; never store raw token
  expires_at      TIMESTAMPTZ
  revoked_at      TIMESTAMPTZ    -- null if still valid
  created_at      TIMESTAMPTZ
```

**Stock level per location** is computed: `SUM(quantity WHERE movement brings stock IN to location) - SUM(quantity WHERE movement takes stock OUT of location)`, split by `container_status` for refillable products.

**Customer outstanding debt** is computed: `SUM(Transactions.total_amount - Transactions.paid_amount WHERE customer) - SUM(DebtPayments.amount WHERE customer)`.

**Container loans outstanding** per customer per product: `SUM(ContainerLoans.quantity WHERE customer AND product)`.
Net > 0 → customer holds our unreturned filled containers. Net < 0 → we hold customer's excess empty containers (we owe them that many filled containers on the next delivery). Net = 0 → balanced.

**Vendor exchange** is an API-level concept handled by `POST /api/stock/vendor-exchange`. It atomically creates two `StockMovements` records: a `transfer` out (empties sent to vendor, `to_location_id = null`) and a `receive` in (filled stock from vendor, `from_location_id = null`). The `purchase_cost` is recorded on the receive movement and used for the daily purchasing cost dashboard stat.

---

## 5. API Endpoints

```
POST   /api/auth/login
POST   /api/auth/refresh
POST   /api/auth/logout

# Profile (all roles — own record only)
GET    /api/profile           -- returns { id, name, username, role } for the authenticated user
PUT    /api/profile           -- update own name and/or password; body: { name?, current_password?, new_password? }
                              -- current_password required when new_password is provided
                              -- role and username are immutable via this endpoint

# Owner only
GET    /api/users
POST   /api/users
PUT    /api/users/{id}
DELETE /api/users/{id}          (soft delete: is_active = false)

GET    /api/locations
POST   /api/locations
PUT    /api/locations/{id}

GET    /api/products
POST   /api/products
PUT    /api/products/{id}
DELETE /api/products/{id}

GET    /api/customers
POST   /api/customers
PUT    /api/customers/{id}
GET    /api/customers/{id}/pricing
PUT    /api/customers/{id}/pricing
GET    /api/customers/{id}/debt    -- outstanding debt summary
GET    /api/customers/{id}/container-loans  -- borrowed containers

# Stock
GET    /api/stock/levels           -- all locations; ?location_id=<id> to filter
                                   -- response shape (per row): { product_id, product_name, product_unit, product_category,
                                   --   location_id, location_name,
                                   --   quantity_filled: number|null,  -- null for simple products
                                   --   quantity_empty: number|null,   -- null for simple products
                                   --   quantity_total: number|null }  -- null for refillable products
GET    /api/stock/movements
                                   -- ?date=YYYY-MM-DD (optional, defaults to today WIB)
POST   /api/stock/movements        -- receive, defect (single item)
POST   /api/stock/movements/bulk   -- receive multiple products into the same destination in one request
                                   -- body: { movement_type: 'receive', to_location_id, notes?, items: [{ product_id, quantity, container_status?, purchase_cost? }] }
                                   -- server creates one StockMovements record per item atomically
POST   /api/stock/transfer         -- warehouse ↔ truck (loading / return) (single item)
POST   /api/stock/transfer/bulk    -- transfer multiple products between the same from/to pair in one request
                                   -- body: { from_location_id, to_location_id, notes?, items: [{ product_id, quantity, container_status? }] }
                                   -- server creates one StockMovements record per item atomically
POST   /api/stock/vendor-exchange      -- atomic vendor exchange: empties out + filled in + purchase cost (owner/kurir)
POST   /api/stock/vendor-exchange/bulk -- exchange containers with vendor for multiple products in one operation (owner/kurir)
                                   -- body: { location_id, notes?, items: [{ product_id, empty_quantity, filled_quantity, purchase_cost }] }
                                   -- server creates two StockMovements records per item atomically (same as vendor-exchange but batched)
POST   /api/stock/production       -- in-house refill (owner and kasir): atomically empty -= qty, filled += qty; product must be refillable + selfproduced

# Delivery Assignments
GET    /api/assignments             -- owner/kasir: all; kurir: own only
POST   /api/assignments             -- create task assignment (owner/kasir only); zero stock side effects
POST   /api/assignments/{id}/fulfill -- kurir processes assignment: creates delivery transaction, marks fulfilled
PUT    /api/assignments/{id}/cancel  -- mark assignment cancelled (owner/kasir only); zero stock side effects

# Transactions
GET    /api/transactions            -- owner: all | kurir/kasir: own only; ?date=YYYY-MM-DD (optional, defaults to today WIB)
POST   /api/transactions            -- delivery or counter
                                   -- body: { type, customer_id?, location_id, items: [{product_id, quantity, unit_price}],
                                   --         paid_amount, payment_method, notes?,
                                   --         container_returns?: [{product_id, quantity}],  -- empty containers returned by customer at delivery
                                   --         debt_payment_amount?: number }               -- optional: settle old debt alongside this transaction
                                   -- status always set to 'completed' on creation
                                   -- server creates: Transaction + TransactionItems + dispatch StockMovements + ContainerLoans (if refillable + customer)
                                   --                + receive StockMovements for container returns + DebtPayments if debt_payment_amount > 0
PUT    /api/transactions/{id}/status  -- only valid target: 'cancelled'
                                   -- server creates compensating receive StockMovements + reverse ContainerLoans for all items
                                   -- authorization: owner can cancel any; kurir/kasir can cancel own only
GET    /api/transactions/{id}
POST   /api/transactions/{id}/payments  -- record partial/full payment on transaction

# Container loans
GET    /api/container-loans         -- owner: all; filter by customer
POST   /api/container-loans         -- record loan event or return event

# Debt
GET    /api/debt-payments
POST   /api/debt-payments           -- standalone customer debt settlement

GET    /api/customers/{id}/debt-history   -- owner/kasir; per-customer debt detail
                                   -- response: CustomerDebtHistory {
                                   --   customer_id, customer_name, outstanding_debt,
                                   --   debt_transactions: [{ id, created_at, type, total_amount, paid_amount, debt_amount, created_by_name }],
                                   --   payments: [{ id, amount, notes, created_by_name, created_at }]
                                   -- }  (both arrays sorted newest first)

GET    /api/cash-flow               -- owner only; ?date=YYYY-MM-DD (optional, defaults to today WIB)
                                   -- aggregates three sources for the given date:
                                   --   1. Transactions: paid_amount → cash_in/sale_payment entry; debt_amount (if >0) → new_debt/debt_created entry
                                   --   2. DebtPayments: amount → cash_in/debt_payment entry
                                   --   3. StockMovements with purchase_cost > 0 → cash_out/stock_purchase entry
                                   -- response: CashFlowSummary {
                                   --   total_cash_in, total_cash_out, net_cash, total_new_debt,
                                   --   entries: CashFlowEntry[] (sorted newest first)
                                   -- }
                                   -- CashFlowEntry: { id, flow_type, category, amount, description, reference_id?, created_by_name, created_at }

GET    /api/dashboard               -- owner only, polled summary; ?date=YYYY-MM-DD (optional, defaults to today WIB)
                                   -- response includes: summary stats for selected date, weekly_chart[7] ({ date, revenue, transaction_count, purchase_cost }),
                                   --   recent_transactions (for selected date), warehouse_stock (current state, not date-filtered),
                                   --   previous_day_revenue, customer_debts (current state, not date-filtered):
                                   --   customer_debts: [{ customer_id, customer_name, outstanding_debt }] — active customers with debt > 0, sorted by debt desc
GET    /api/health
```

---

## 6. Frontend Structure

```
src/
├── assets/                  -- images, fonts
├── styles/
│   ├── _variables.scss      -- colors, spacing, breakpoints
│   ├── _mixins.scss         -- reusable SCSS mixins
│   ├── _reset.scss          -- normalize/reset
│   └── global.scss          -- import all partials
├── components/
│   ├── common/              -- Button, Input, Modal, Table, BottomSheet, etc.
│   └── layout/              -- Navbar, BottomNav (all roles use mobile nav)
├── pages/
│   ├── Login/
│   ├── Dashboard/           -- owner only
│   ├── Lainnya/             -- owner only; route: /lainnya; hub linking to Users, Products, Locations, DebtPayments, CashFlow, Profile, Logout (renamed from Settings/)
│   ├── Profile/             -- all roles; view/edit own name and password; logout (kurir/kasir)
  ├── Stock/               -- StockLevels (all roles); Riwayat tab (all roles); Kontainer tab (owner only); Terima/Tukar Agent/Defek (owner only); Transfer + Produksi (owner and kasir)
  ├── Transactions/        -- TransactionList, 3-step create overlay (DeliveryForm kurir / CounterSaleForm kasir/owner); Penugasan tab (all roles); assignment creation overlay (owner/kasir); fulfillment overlay (kurir)
│   ├── Customers/           -- CustomerList, CustomerDebt, ContainerLoans
│   ├── DebtPayments/        -- two tabs: Hutang Aktif (clickable rows → CustomerDebtDetailPage) + Riwayat (date-filtered payment history)
│   │   └── CustomerDebtDetailPage -- per-customer debt detail at /debt-payments/:customerId (owner/kasir)
│   ├── CashFlow/            -- owner only; date-filtered cash flow summary + entry list at /cash-flow
│   ├── Locations/           -- LocationList (warehouse + trucks)
│   └── Users/               -- owner only
├── hooks/
│   ├── useAuth.js
│   ├── usePolling.js        -- generic polling hook
│   └── useApi.js            -- axios instance with interceptors
├── context/
│   └── AuthContext.jsx
├── services/
│   ├── authService.js
│   ├── stockService.js
│   ├── transactionService.js
│   ├── cashFlowService.ts   -- GET /api/cash-flow; aggregates tx/debt payments/stock movements for a date
│   ├── customerService.js
│   ├── debtService.js
│   └── containerLoanService.js
└── utils/
    ├── formatCurrency.js    -- Rupiah formatting
    └── constants.js
```

### Mobile-first SCSS approach
- **All screens are mobile-first** — Owner, Kurir, and Kasir all use mobile phones as their primary device
- Breakpoints defined in `_variables.scss`: `$mobile: 480px`, `$tablet: 768px`, `$desktop: 1024px`
- Base styles written for mobile, desktop overrides added via `@media (min-width: $tablet)`
- Bottom navigation bar used for all roles on mobile (no sidebar)
- All interactive elements: min 44px tap targets, bottom-anchored action buttons, large input fields (min 16px font to prevent iOS auto-zoom)

### PWA (optional, minimal effort)
Add `manifest.json` + service worker via Vite plugin so couriers can install the app to their Android home screen without a Play Store listing.

---

## 7. Security Plan

### 7.1 Authentication & Authorization

| Control | Implementation |
|---|---|
| Password hashing | BCrypt with cost factor 12 (via `BCrypt.Net-Next`) |
| Tokens | JWT access token (60 min expiry) + refresh token (7 days, stored in HttpOnly cookie) |
| Role-based access | ASP.NET Core `[Authorize(Roles = "owner")]` policy on protected controllers |
| Token refresh | `/api/auth/refresh` validates refresh token from HttpOnly cookie, issues new access token |
| Logout | Refresh token invalidated server-side (stored in DB, marked revoked on logout) |

**Refresh token storage:** Store in `RefreshTokens` table (user_id, token_hash, expires_at, revoked_at). Never store raw token — store SHA-256 hash.

### 7.2 API Security

| Threat | Mitigation |
|---|---|
| Brute-force login | Rate limiting on `/api/auth/login`: max 10 attempts per IP per 15 min (ASP.NET Core rate limiting middleware) |
| SQL injection | EF Core parameterized queries by default; no raw SQL unless absolutely necessary, and only with `FromSqlInterpolated` |
| Mass assignment | Use explicit DTOs for all input — never bind directly to entity models |
| IDOR (access other user's data) | Couriers: filter all queries by `courier_id = currentUserId` at service layer |
| Sensitive data in logs | Never log passwords, tokens, or full connection strings |
| CORS | Allow only `https://your-app.vercel.app` in production — no wildcard `*` |
| HTTPS | Enforced at MonsterASP.NET (Let's Encrypt) and Vercel — `UseHttpsRedirection()` in API |
| Security headers | Add via middleware: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin` |

### 7.3 Database Security

| Control | Implementation |
|---|---|
| Connection | TLS required (`sslmode=require` in connection string to Neon) |
| Credentials | Connection string stored only in MonsterASP.NET environment config, never in source code or git |
| Principle of least privilege | Neon DB user has only DML permissions (SELECT/INSERT/UPDATE/DELETE) — no DDL in production |
| Secrets in code | `.gitignore` covers `appsettings.Production.json`; use `appsettings.Development.json` for local dev only |

### 7.4 Frontend Security

| Threat | Mitigation |
|---|---|
| XSS | React escapes output by default; avoid `dangerouslySetInnerHTML`; Content-Security-Policy header |
| Token storage | Access token in memory (React state/context) only — not localStorage (XSS risk) |
| Refresh token | HttpOnly, Secure, SameSite=Strict cookie — JS cannot access it |
| API key exposure | Only `VITE_API_BASE_URL` in frontend env (public URL, not secret) |
| CSRF on refresh | SameSite=Strict cookie prevents cross-site refresh token abuse |

### 7.5 Input Validation

- **API layer**: Use `[Required]`, `[MaxLength]`, `[Range]` data annotations + `FluentValidation` for complex rules
- **Frontend layer**: Client-side validation for UX only — API always validates independently
- **Currency/quantity**: Decimal precision enforced; negative quantities rejected at API level
- **Enum inputs**: Validated against allowed values; reject unknown strings

### 7.6 OWASP Top 10 Coverage (Phase 1)

| # | Risk | Status |
|---|---|---|
| A01 | Broken Access Control | ✅ Role policies + IDOR filtering at service layer |
| A02 | Cryptographic Failures | ✅ BCrypt passwords, TLS everywhere, HttpOnly tokens |
| A03 | Injection | ✅ EF Core parameterized queries, DTO binding |
| A04 | Insecure Design | ✅ Principle of least privilege, refresh token revocation |
| A05 | Security Misconfiguration | ✅ CORS whitelist, HTTPS enforced, headers middleware |
| A06 | Vulnerable Components | ⚠️ Keep NuGet/npm packages updated; run `dotnet list package --vulnerable` |
| A07 | Auth Failures | ✅ Rate limiting, token expiry, revocation |
| A08 | Software/Data Integrity | ✅ JWT signature verification; no untrusted deserialization |
| A09 | Logging & Monitoring | ⚠️ Basic logging in Phase 1; structured logging (Serilog) recommended for Phase 2 |
| A10 | SSRF | ✅ No outbound HTTP calls to user-supplied URLs in Phase 1 |

---

## 8. Phased Delivery Plan

### Phase 1A — Foundation (do first, blocks everything)
1. Scaffold ASP.NET Core Web API project (.NET 10 LTS, Controllers)
2. Configure EF Core + Npgsql, connect to Neon Singapore
3. Create all DB migrations (12 entities: Users, Locations, Products, Customers, CustomerPricing, StockMovements, Transactions, TransactionItems, Payments, DebtPayments, ContainerLoans, RefreshTokens)
4. Implement JWT auth middleware, BCrypt, role policies (`owner`, `kurir`, `kasir`)
5. `POST /api/auth/login` + `POST /api/auth/refresh`
6. CORS config, HTTPS redirect, security headers middleware
7. `GET /api/health` endpoint

### Phase 1B — Feature Modules (parallel after 1A)
8. Users CRUD (Owner only) — roles: owner, kurir, kasir
9. Locations CRUD (warehouse + vehicles, assigned kurir)
10. Products CRUD — category (simple/refillable), production_type, type (air/gas)
11. Customers CRUD + CustomerPricing
12. Stock movements — receive, transfer (truck loading/return), defect write-off, vendor exchange (owner/kurir)
13. Computed stock levels per location (warehouse + each vehicle), split filled/empty for refillable
14. Transactions — delivery (kurir), counter (kasir), vendor_direct (pass-through)
15. TransactionItems + price snapshot
16. Payments — partial payment support (paid_amount field, multiple payment events per transaction)
17. ContainerLoans — record lent/returned containers per customer
18. DebtPayments — standalone debt settlement, computed customer outstanding balance
19. Dashboard summary endpoint

### Phase 1C — Frontend (parallel with 1B)
20. React + Vite project, SCSS structure (`_variables`, `_mixins`, `_reset`, `global`)
21. Axios instance with JWT interceptor + auto-refresh logic
22. Auth context + Login page (mobile-first)
23. Bottom nav layout for all roles (mobile primary)
24. Owner: Dashboard (polling stock + recent transactions)
25. Owner: Stock levels per location (warehouse + trucks)
26. Owner: Stock receive form + defect write-off form
27. Owner/Kurir: Truck loading form (warehouse → truck transfer)
28. Owner/Kurir: Truck return form (truck → warehouse, mixed empty/filled)
29. Owner: Customer management + pricing
30. Owner: Container loans view per customer
31. Owner: Customer debt view + debt payment form
32. Owner: User management + location (vehicle) management
33. Kurir: Delivery transaction form (from truck stock, 3-step: customer → products → review)
34. Kasir: Counter sale form (from warehouse stock, same 3-step flow)
35. All roles: Vendor-direct pass-through transaction form
36. All roles: Partial payment input on transaction submit

### Phase 1D — Deploy & Verify
37. Deploy API to MonsterASP.NET (Visual Studio publish or FTP)
38. Set all environment variables (connection string, JWT secret)
39. Deploy React to Vercel, set `VITE_API_BASE_URL`
40. Verify: login (all 3 roles), truck loading, delivery by kurir, counter sale by kasir, partial payment + debt visible on owner dashboard within 10s

### Phase 2 — Payment Gateway (future)
- Integrate **Midtrans** or **Xendit** (both Indonesian, have .NET SDKs)
- Generate QRIS code per transaction
- Webhook `POST /api/payments/webhook` updates `Payments.status` on callback
- Add webhook signature verification (HMAC from gateway)

### Phase 3 — Mobile App & Offline Support (future)
- React Native or Flutter consuming the same API (no API changes needed if designed correctly)
- Courier app as primary target
- Offline transaction drafting: queue transactions locally (IndexedDB / device storage) when no network, sync automatically on reconnection
- Conflict resolution strategy for offline-queued transactions (e.g., stock already depleted) to be defined in Phase 3 FRD

#### Stock Level Performance: Snapshot + Movement Pattern
Phase 1 computes stock levels via `SUM` aggregation over `StockMovements` (FR-STK-001). This is correct by design (no sync risk) but becomes slow as the movement table grows. Phase 3 introduces a `StockBalances` snapshot table to keep reads O(1):

```
StockBalances
  location_id       UUID  FK → Locations
  product_id        UUID  FK → Products
  container_status  ENUM('filled', 'empty', 'na')
  quantity          INT              -- current running balance
  updated_at        TIMESTAMPTZ
  PRIMARY KEY (location_id, product_id, container_status)
```

**How it works:**
- Every `StockMovements` insert atomically updates the corresponding `StockBalances` row(s) in the **same DB transaction** — ACID guarantees both records change together or neither does
- `GET /api/stock/levels` switches to read from `StockBalances` directly — no aggregation query needed
- `StockMovements` is still written in full and retained for auditing/history

**Migration steps (no downtime):**
1. Add `StockBalances` table via EF Core migration
2. Seed it by running the existing SUM queries once (`INSERT INTO StockBalances SELECT ... FROM StockMovements GROUP BY ...`)
3. Update the `StockMovements` write path to also `UPDATE StockBalances SET quantity = quantity ± ? WHERE ...` in the same transaction
4. Switch `GET /api/stock/levels` to read from `StockBalances`
5. Keep the SUM aggregation path available under a feature flag until the snapshot is verified correct

**Trigger:** Migrate when `StockMovements` exceeds ~50,000 rows or when the 5-second polling (FR-STK-002) causes measurable DB load.

## 9. Open Decisions / To Revisit

| # | Decision | Current Plan | Revisit Trigger |
|---|---|---|---|
| 1 | MonsterASP.NET traffic limits | Unknown (free tier says "limited") | If API calls start getting throttled |
| 2 | Neon 0.5 GB storage | Sufficient for small MSMe | When product catalogue + transactions approach 300 MB |
| 3 | Polling vs. WebSocket/SSE | Polling (5–10s) | If owner needs sub-second live updates |
| 4 | Report/export feature | Not in scope Phase 1 | Owner requests transaction export to Excel/PDF |
| 5 | Multi-branch | Single store assumed | If business expands to multiple locations |
