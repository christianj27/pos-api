# POS App — Functional Requirements Document (FRD)
> MSMe Water & Gas | Version 2.4 | May 7, 2026

---

## Revision History

| Version | Date | Author | Changes |
|---|---|---|---------|
| 4.3 | May 25, 2026 | — | FR-CST-008 — Konfidensial Pelanggan: Owner dapat menandai pelanggan sebagai konfidensial menggunakan toggle checkbox pada form buat/edit pelanggan. Pelanggan konfidensial hanya terlihat oleh owner — kasir dan kurir tidak dapat melihatnya di daftar `CustomersPage` maupun di pemilih pelanggan (Langkah 1) `TransactionsPage`. Filter diterapkan server-side: `GET /api/customers` menyaring `is_confidential = true` untuk pemanggil non-owner. Owner melihat badge "Konfidensial" (pink) pada kartu pelanggan di daftar. Backend: field `IsConfidential` (boolean, default `false`) ditambahkan ke model `Customer`; field disertakan di `CustomerResponse`, `CreateCustomerRequest`, `UpdateCustomerRequest`; `CustomerService.GetAllAsync` menerima parameter `userRole` dan menerapkan filter kondisional; `CustomersController` mengambil role dari JWT claims dan meneruskannya ke service; perubahan `is_confidential` dalam `PUT /api/customers/{id}` diabaikan untuk non-owner. EF migration baru: `AddConfidentialToCustomer`. Frontend: field `isConfidential?` ditambahkan ke tipe `Customer`; `customerService.list(role?)` menerima role untuk simulasi filter mock; `CustomersPage` menampilkan checkbox "Konfidensial" (owner-only) di form dan badge pada kartu; `TransactionsPage.load()` meneruskan role ke `customerService.list()`. |
| 4.2 | May 22, 2026 | — | FR-DSH-011 — Dashboard accessible to all roles (owner, kasir, kurir). Kasir/kurir restrictions: Biaya Pembelian stat card hidden; Biaya Pembelian row in bar chart detail panel hidden; Pendapatan per Staf pie chart section hidden. All transaction-derived stats (Pendapatan, Transaksi, Biaya Pembelian, Piutang Terbayar, Pendapatan 7 Hari, Transaksi Terkini) scoped server-side to the authenticated user for kasir/kurir. Hutang Pelanggan, Stok Gudang, Stok Rendah shown store-wide for all roles. Backend: `DashboardController` authorization changed from `OwnerOnly` to `Authorize` (all authenticated); `userId` and `role` extracted from JWT claims; `DashboardService.GetDashboardAsync` accepts `userId` + `role`, uses `IQueryable<T>` with conditional `.Where(t => t.StaffId == userId)` / `.Where(m => m.CreatedBy == userId)` for non-owners; `staffRevenue` always empty for kasir/kurir. Frontend: `/dashboard` route allows `['owner','kasir','kurir']`; BottomNav adds Dashboard as first item for kasir/kurir (4→5 items each); `DashboardPage` uses `useAuth()` and conditionally hides owner-only sections. |
| 4.1 | May 21, 2026 | — | FR-ASG-LOC — Assignment location selection: Owner/Kasir must pick a stock source location (any active warehouse or vehicle) in Step 1 of assignment creation. The chosen location is stored on the assignment (`location_id`) and pre-filled + locked as the Lokasi Stok when Kurir fulfills the assignment in the transaction overlay. Kurir's own new (non-assignment) delivery transactions continue to auto-fill from the kurir's assigned vehicle as before. Backend: `location_id` (nullable UUID FK) added to `DeliveryAssignments`; `CreateAssignmentRequest` extended with required `LocationId`; `AssignmentResponse` returns `location_id` + `location_name`; `FulfillAsync` uses stored location with vehicle fallback for old records. New EF migration `AddLocationToAssignment`. Frontend: `DeliveryAssignment` type extended; `assignmentService` updated; `TransactionsPage` Step 1 gains a location picker and the Step 2 locked fields show Lokasi Stok. |
| 4.0 | May 21, 2026 | — | FR-DSH-010 — Pendapatan per Staf pie chart: Owner sees a pie chart below "Pendapatan 7 Hari" on the Dashboard showing revenue (`paid_amount`) per staff member for the selected date. Only completed transactions are counted. Each slice is labelled with the staff name and percentage share; tooltip shows formatted currency + percentage. Empty state shown when no revenue exists for the selected date. Backend `GET /api/dashboard` response extended with `staff_revenue` array (`staff_id`, `staff_name`, `revenue`, `transaction_count`); sorted by revenue descending. No new endpoint. Frontend: `PieController`, `ArcElement`, `Legend` added to Chart.js registry; new `StaffRevenueSummary` type; mock computes from `recentTransactions` seed data. |
| 3.9 | May 20, 2026 | — | FR-CSH-005 — Monthly cash flow export to XLSX: Owner can pick a calendar month (month/year picker, default = current month, max = current month) on the Arus Kas page and download a `.xlsx` report covering the entire calendar month. Report is generated client-side using SheetJS (`xlsx`). Two sheets: **Ringkasan Bulanan** (daily breakdown table: Tanggal, Kas Masuk, Kas Keluar, Net Kas, Piutang Baru — one row per day, total row at bottom, preceded by aggregate summary block) and **Detail Transaksi** (all individual entries sorted oldest first: No, Tanggal, Waktu, Jenis Arus, Kategori, Keterangan, Dicatat Oleh, Jumlah). File name: `LaporanArusKas_YYYY-MM_diekspor_YYYYMMDD.xlsx`. Backend `GET /api/cash-flow` extended with optional `start_date` + `end_date` query params (YYYY-MM-DD); when both present, returns aggregate summary for the full range — `date` param is ignored. Same `CashFlowSummaryResponse` DTO reused; no new endpoint or type needed. New utility: `src/utils/cashFlowExport.ts`. New service method: `cashFlowService.getRange(startDate, endDate)`. |
| 3.8 | May 20, 2026 | — | FR-DBT-007 — Initial customer outstanding debt: `initial_debt` field added to Customer. Owners can set a one-time opening balance when creating or editing a customer. `outstanding_debt` formula updated to `initial_debt + SUM(transaction debt_amounts) - SUM(debt_payments)`. `CustomerDebtHistory` response now includes `initial_debt` field; `CustomerDebtDetailPage` shows an amber "Saldo Awal Hutang" info row when `initial_debt > 0`. New EF migration `AddInitialDebtToCustomer`. |
| 3.7 | May 19, 2026 | — | FR-STK-016 — Auto-populate Transfer tab from vehicle stock: when the user selects a vehicle as the "Dari Lokasi", the item list is automatically pre-filled with all products currently stocked on that vehicle (using already-loaded stock levels data). Simple products populate with `quantity_total`; refillable products populate one row per status (filled / empty) with qty > 0. An info banner appears below the location selector. Switching to a warehouse resets the list to a single blank row. Pure frontend enhancement — no new API endpoints. |
| 3.6 | May 19, 2026 | — | Two improvements: (1) FR-STK-015 — Negative stock warning for Transfer tab: frontend checks transfer quantities against current stock levels at the source location; if any item would result in negative stock, a confirmation dialog (soft warning) is shown before submitting — user may still proceed; (2) FR-TXN-021 — Negative stock warning on transaction creation: same pattern applied in Langkah 3 "Kirim Transaksi"; frontend checks each cart item against stock levels at the selected location; applies to both direct creation and Kurir fulfillment flows. |
| 3.5 | May 19, 2026 | — | Three improvements: (1) FR-STK-014 — Stock movement Riwayat tab now shows customer name instead of empty destination for dispatch movements (backend includes `customer_name` from Transaction.Customer; `GET /api/stock/movements` response adds `customer_name` field); (2) FR-TXN-015 updated — Date filter is now shared across all tabs including Penugasan; `GET /api/assignments` accepts optional `date` query param (YYYY-MM-DD WIB); Penugasan tab uses the same `selectedDate` state as the transaction list; (3) FR-MST-005/006/007 — Aktifkan button added to Customer, Location, and Product management pages allowing reactivation of inactive records (Owner only; warehouses excluded). |
| 3.4 | May 19, 2026 | — | Three improvements: (1) FR-STK-014 — Stock movement dispatch shows customer name in route (from→customer); (2) FR-TXN-015 — `GET /api/assignments` date filter added; Penugasan tab gets its own date filter UI; (3) FR-MST-005/006/007 — Aktifkan (reactivation) button for Customer, Location, Product pages. |: Owner/Kasir create task assignments for Kurir (zero stock side effects); Penugasan tab added to Transactions page (Kurir defaults to this tab); 2-step assignment creation overlay (Step 1: select Kurir + Customer; Step 2: select products + notes); Kurir processes via fulfillment overlay → creates delivery transaction; cancel confirm dialog for assignments; `assignmentService.ts` + mock seed records added; (2) Stock tab permission changes — Kurir: levels + movements only; Kasir: levels + movements + transfer + production; FR-STK-011 (production) expanded from Owner-only to Owner + Kasir; (3) FR-STK-012 UI — purchase cost hidden for `vendor_exchange` movements from Kurir and Kasir roles in the Riwayat tab (purchase cost is owner-only business information; all other movement types unaffected). |
| 3.2 | May 10, 2026 | — | Four changes: (1) Route renamed from `/settings` to `/lainnya` throughout code and live spec; updated FR-SET-001, FR-PRF §5.4 UI logout note, FR-CSH-001 back button; (2) Lainnya bottom nav icon changed from gear (`SettingsIcon`) to 2×2 grid icon (`LainnyaIcon` — more appropriate for a "more/others" hub menu); (3) DebtPayments tabs restyled from underline to pill pattern matching StockPage/TransactionsPage convention; (4) Mock debt payment seed data added for 2026-05-10 (debt-4, debt-5, debt-6) so Riwayat tab shows data on default today filter. |
| 3.1 | May 10, 2026 | — | Four enhancements: (1) FR-SET-001 — Bottom nav item "Pengaturan" renamed to "Lainnya"; Settings hub page H1 title updated; frontend folder/files renamed from `Settings/*` to `Lainnya/*`; (2) FR-DBT-001–005 — DebtPayments page refactored into two tabs: "Hutang Aktif" (active customers with outstanding_debt > 0 sorted by debt desc, rows clickable to per-customer detail sub-page at `/debt-payments/:customerId` showing debt-creating transactions and standalone payments, back button returns to list, "+ Catat Pembayaran" button scoped to this tab) and "Riwayat" (standalone debt payment history with date filter, lazy-loaded on first open); Dashboard "Hutang Pelanggan" section rows now clickable navigating to `/debt-payments/:customerId`; new `GET /api/customers/{id}/debt-history` endpoint; (3) FR-CSH-001–004 — New Arus Kas (Cash Flow) page at `/cash-flow` (owner only), accessible from Lainnya hub; date filter (input + "Hari Ini" shortcut, default today WIB); four summary cards (Kas Masuk green, Kas Keluar red, Net Kas blue/red, Piutang Baru amber); entry list with three flow types: `cash_in` (green — from transaction paid_amount and standalone debt payments), `cash_out` (red — from stock purchase_cost), `new_debt` (amber — from unpaid transaction debt_amount, labelled "Piutang Baru"); new `GET /api/cash-flow` endpoint; new types `CashFlowEntry`, `CashFlowSummary`, `CashFlowType`, `CashFlowCategory`, `DebtTransaction`, `CustomerDebtHistory`. |
| 3.0 | May 10, 2026 | — | Three enhancements: (1) FR-STK-013 — Vendor Exchange tab upgraded to multi-product bulk input (mirrors FR-STK-004/005 Receive/Transfer pattern): shared Lokasi field, per-item product+quantities+cost card, add/remove rows, shared Catatan, Total Biaya Pembelian summary row, single submit; new bulk API `POST /api/stock/vendor-exchange/bulk`; (2) FR-DSH-008 — Recent transaction rows in Dashboard are now clickable and open a Detail Transaksi modal (same data model as Transactions page detail view); (3) FR-DSH-009 — New "Hutang Pelanggan" section below Transaksi Terkini in Dashboard listing active customers with outstanding debt > 0, sorted by debt descending; date-filter-independent (always current state, same as Stok Gudang); new `customer_debts` field added to `GET /api/dashboard` response; new `CustomerDebtSummary` type. |
| 2.9 | May 10, 2026 | — | Date filter added to transaction list and stock movement history: FR-TXN-015 (`GET /api/transactions` accepts optional `date` query param, YYYY-MM-DD WIB, defaults to today WIB); FR-STK-012 (same for `GET /api/stock/movements`). UI matches dashboard FR-DSH-006 pattern: date `<input type="date">` + conditional "Hari Ini" shortcut button, default = today WIB. Movements tab now lazy-loaded on first open (mirroring Kontainer tab pattern). Mock layer filters in-memory. Seed dates for tx-6, tx-7, mov-7–10 updated to 2026-05-10. |
| 2.8 | May 9, 2026 | — | Dashboard enhancements: FR-DSH-002 replaces `today_new_debt` with `today_debt_collected` (sum of DebtPayments today) and adds `previous_day_revenue` for revenue % delta; FR-DSH-007 upgraded to Chart.js interactive bar chart — each bar clickable to show inline detail panel (revenue, transaction count, purchase cost, avg per transaction for that day); `WeeklyChartEntry` extended with `transaction_count` and `purchase_cost` fields. ARCHITECTURE.md updated with chart.js + react-chartjs-2 in tech stack. |
| 2.7 | May 9, 2026 | — | Container loan concept clarification: FR-CON module rewritten with explicit quantity semantics (positive = filled delivered to customer; negative = empty returned by customer; net < 0 means we owe the customer); Kontainer tab now shows **both** positive-net (customer owes us) and negative-net (we owe customer) rows with distinct visual styling; transaction form Step 3 container-returns section title and hint text updated; transactionService mock now creates dispatch/receive StockMovements for every transaction; realistic exchange scenario mock data added (tx-6, tx-7). |
| 2.6 | May 9, 2026 | — | Transaction overhaul: removed `vendor_direct` type and `pending` status — all transactions created with `status = 'completed'`; Step 1 customer now optional for Kasir/Owner (anonymous sales); Tipe Transaksi and Lokasi Stok locked and auto-filled for Kurir/Kasir in Step 2; inline editable quantity input in Step 2; Step 3 shows customer outstanding debt in red, optional "Bayar Hutang Lama" field (creates DebtPayments record), and "Kembalian Kontainer" section per refillable product; cancel always auto-reverses stock and ContainerLoans; new FR-TXN-011–013 (refillable handling, anonymous sale, status model); new "Kontainer" tab in StockPage (Owner only) for standalone container return management (FR-CON-005). |
| 2.5 | May 9, 2026 | — | Stock UX enhancements: Transfer tab now shows a descriptive subtitle (FR-STK-005/006 UI). Receive tab (FR-STK-004) and Transfer tab (FR-STK-005/006) now support bulk multi-product input in a single submission. Shared location + shared note per batch; per-item purchase cost on Receive. New bulk API endpoints: `POST /api/stock/movements/bulk` and `POST /api/stock/transfer/bulk`. |
| 2.4 | May 7, 2026 | — | Stock module updates: kasir now has read-only levels tab access; kurir can access levels, movements, transfer, and vendor exchange tabs; levels display aggregated one-row-per-product with unit-based empty pool summary rows; new FR-STK-011 (in-house production refill); controlled vocabulary for product unit field (FR-PRD-002, VAL-PRD-005). |
| 2.3 | May 7, 2026 | — | Profile management module (FR-PRF): all roles can update own name and password via `PUT /api/profile`. New `/profile` frontend page; `/settings` hub for owner. Transaction Step 3 payment method defaults to `cash`. Bottom nav max 5 items per role; logout moved to `/settings` (owner) and `/profile` (kurir/kasir). |
| 2.2 | May 7, 2026 | — | Dashboard date filter (FR-DSH-006) and weekly revenue bar chart (FR-DSH-007). `GET /api/dashboard` accepts optional `date` query param (ISO 8601, defaults to today WIB); response includes `weekly_chart` array (7 days ending on selected date). Updated user stories, UI behavior, and API endpoint spec. |
| 2.1 | May 6, 2026 | — | Vendor exchange flow: owner/kurir takes empties to vendor, receives filled stock, records purchase cost. FR-STK-010, VAL-STK-008, dashboard purchase stat, permission matrix row. Relax FR-STK-005 (empty containers may be loaded), FR-STK-004 (receive targets any location), FR-LOC-002/VAL-LOC-003 (assigned_to: owner or kurir). |
| 2.0 | May 6, 2026 | — | Major domain expansion: 3 roles (owner/kurir/kasir), Locations/truck stock, container filled/empty tracking, 3 transaction types, defect write-off, container loans, partial payment + customer debt model. All screens mobile-first. |
| 1.1 | May 6, 2026 | — | Phone optional; all user-facing text in Bahasa Indonesia |
| 1.0 | May 6, 2026 | — | Initial draft, Phase 1 scope |

---

## 1. Introduction

### 1.1 Purpose
This document defines the functional requirements for the POS App Phase 1 build. It serves as the primary reference for development decisions, implementation details, and acceptance criteria.

### 1.2 Scope — Phase 1
- Authentication (login, token refresh, logout) — 3 roles: owner, kurir, kasir
- User management (owner only)
- Location management (warehouse + vehicles/trucks)
- Product management — simple vs. refillable categories
- Customer management with per-customer pricing
- Stock management: receive, transfer (truck loading/return), defect write-off, computed per-location levels
- Vendor exchange: owner/kurir takes empties to vendor, receives filled stock, records purchase cost
- Transactions: delivery (kurir), counter sale (kasir); customer optional for Kasir/Owner (anonymous walk-in sales)
- Partial payment + customer debt model; old debt optionally settled inline at Step 3 or via standalone DebtPayments
- Container loan tracking during transaction flow (Step 3 container return) and standalone via StockPage Kontainer tab
- All transactions created with `status = 'completed'` — no pending state
- Owner dashboard (summary + polling)
- PWA home-screen install (manifest + service worker via Vite plugin)

### 1.3 Out of Scope (Phase 1)
- **QRIS / payment gateway integration** — Phase 2 (Midtrans / Xendit)
- **Offline transaction drafting and sync** — Phase 3
- **Mobile app (React Native / Flutter)** — Phase 3
- **Transaction export to Excel / PDF** — deferred
- **Multi-branch support** — deferred
- **Structured logging (Serilog)** — Phase 2
- **Individual container serial tracking** — deferred (count-only in Phase 1)

### 1.4 Related Documents
- [ARCHITECTURE.md](ARCHITECTURE.md) — tech stack, domain model, API endpoints, security, deployment
- [DESIGN.md](DESIGN.md) — design tokens, component styles, typography, color system

---

## 2. System Overview

The POS App is a web-based point of sale system for a single-store MSMe selling bottled water and cylinder gas. All users (Owner, Kurir, Kasir) access the system via mobile browser as their primary device.

### 2.1 User Roles

| Role | Responsibilities |
|---|---|
| **Owner** | Full access: dashboard, stock, locations, customers, users, all transactions, debt, container loans |
| **Kurir** | Truck loading/return, vendor exchange runs, delivery transactions on-site, partial payment collection |
| **Kasir** | Counter sales at warehouse, partial payment collection |

### 2.2 Product Categories

| Category | Description | Examples |
|---|---|---|
| `simple` | No container; standard in/out stock | Karton air cup |
| `refillable` | Has empty/filled state; containers circulate | Galon air (self-produced), tabung gas (purchased from vendor) |

For `refillable` products, stock is tracked separately as **filled** count and **empty** count per location.

### 2.3 Transaction Types

| Type | Who creates | Stock source |
|---|---|---|
| `delivery` | Owner / Kurir | From assigned truck/vehicle (kurir: auto-assigned vehicle; owner: selects any active vehicle) |
| `counter` | Owner / Kasir | From warehouse |

### 2.4 Authentication Model
- Access token: JWT (HS256), 60-minute expiry, stored in React memory (not localStorage)
- Refresh token: 7-day expiry, stored in HttpOnly Secure SameSite=Strict cookie
- All API routes (except `POST /api/auth/login` and `POST /api/auth/refresh`) require a valid access token

---

## 3. Functional Requirements

Requirements: `FR-[MODULE]-[NNN]`. Validation rules: `VAL-[MODULE]-[NNN]`. Each module includes role access, user stories, functional specs, validation rules, and UI behavior.

---

## 4. Module: Authentication (FR-AUTH)

**Accessible by:** All roles (login page — unauthenticated)

### 4.1 User Stories

- As any user, I want to log in with my username and password so I can access the system.
- As any user, I want my session to automatically extend while I am active within 7 days.
- As any user, I want to log out so my session is securely ended.

### 4.2 Functional Requirements

**FR-AUTH-001 — Login**
The system shall accept a `username` and `password` and, upon valid credentials, return a JWT access token and set a refresh token in an HttpOnly cookie.

**FR-AUTH-002 — Role-based redirect after login**
After successful login, the system shall redirect:
- Owner → `/dashboard`
- Kurir → `/transactions`
- Kasir → `/transactions`

**FR-AUTH-003 — Failed login handling**
The system shall return a generic error message `"Username atau kata sandi salah."` for any failed login attempt. It shall not distinguish between unknown username and wrong password.

**FR-AUTH-004 — Rate limiting**
The system shall limit login attempts to a maximum of **10 per IP address per 15-minute window**. On exceeding this limit, the API returns HTTP 429 and the UI displays: `"Terlalu banyak percobaan login. Silakan tunggu 15 menit dan coba lagi."`

**FR-AUTH-005 — Token refresh**
The system shall silently refresh the access token using the HttpOnly refresh token cookie. The frontend Axios interceptor handles this automatically on 401 responses.

**FR-AUTH-006 — Logout**
On logout, the system shall:
1. Call `POST /api/auth/logout` to mark the refresh token as revoked in the database
2. Clear the access token from React memory
3. Redirect the user to the login page

**FR-AUTH-007 — Expired or revoked session**
If the refresh attempt fails (revoked or expired), the system shall redirect to the login page with the message: `"Sesi Anda telah berakhir. Silakan masuk kembali."`

**FR-AUTH-008 — Already authenticated**
If an authenticated user navigates to `/login`, they shall be redirected to their role-appropriate default page.

### 4.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-AUTH-001 | `username` | Required | `"Username wajib diisi."` |
| VAL-AUTH-002 | `password` | Required | `"Kata sandi wajib diisi."` |

### 4.4 UI Behavior

- Login form: username input + password input (show/hide toggle) + tombol "Masuk"
- Button shows loading spinner while request is in-flight; disabled during request
- On success: redirect (no toast)
- On failure: pesan error inline di bawah form (bukan toast)
- On rate limit: pesan error inline yang sama dengan pesan batas percobaan

---

## 5. Module: Profile Management (FR-PRF)

**Accessible by:** All roles

### 5.1 User Stories

- As any user, I want to view my own name, username, and role so I know which account I am using.
- As any user, I want to update my display name so it reflects my real name.
- As any user, I want to change my own password to keep my account secure.

### 5.2 Functional Requirements

**FR-PRF-001 — View own profile**
The system shall allow any authenticated user to view their own profile: name, username, and role. The profile is read-only except for the fields covered by FR-PRF-002 and FR-PRF-003.

**FR-PRF-002 — Update own name**
All roles can update their own display name via `PUT /api/profile`. The new name is immediately reflected in the UI header.

**FR-PRF-003 — Update own password**
All roles can change their own password via `PUT /api/profile`. The request must include `current_password` (for verification) and `new_password`. If `current_password` does not match, the API returns HTTP 400.

**FR-PRF-004 — Cannot change own role or username via profile**
The profile endpoint does not allow changing `role` or `username`. These are managed by the Owner via FR-USR. Any fields included in the request body other than `name`, `current_password`, and `new_password` are ignored.

### 5.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-PRF-001 | `name` | Required, max 100 chars | `"Nama wajib diisi."` / `"Nama tidak boleh lebih dari 100 karakter."` |
| VAL-PRF-002 | `current_password` | Required when `new_password` is provided | `"Kata sandi saat ini wajib diisi."` |
| VAL-PRF-003 | `current_password` | Must match the user's current hashed password | `"Kata sandi saat ini tidak sesuai."` |
| VAL-PRF-004 | `new_password` | Min 8 chars when provided | `"Kata sandi baru minimal 8 karakter."` |

### 5.4 UI Behavior

- **Profile page (`/profile`) — all roles:**
  - Info section (read-only): nama, username, role badge
  - Edit nama form: current name pre-filled, Save button
  - Ganti kata sandi form: kata sandi saat ini input + kata sandi baru input (show/hide toggle) + konfirmasi kata sandi baru input; Save button
  - Tombol "Keluar" (Logout) — for kurir and kasir only (owner logs out from `/lainnya`)
- **Lainnya page (`/lainnya`) — owner only:**
  - Navigation hub grid: Pengguna, Produk, Lokasi, Hutang, Profil cards
  - Tombol "Keluar" (Logout) as a destructive action card
- **Success:** Toast `"Profil berhasil diperbarui."` / `"Kata sandi berhasil diubah."`
- **Error:** Inline error under the affected field

---

## 6. Module: User Management (FR-USR)

**Accessible by:** Owner only

### 5.1 User Stories

- As an Owner, I want to create accounts for kurir and kasir so they can log into the system.
- As an Owner, I want to edit a user's details or reset their password.
- As an Owner, I want to deactivate a user without deleting their transaction history.
- As an Owner, I want to see a list of all users.

### 5.2 Functional Requirements

**FR-USR-001 — User list**
The system shall display all users (active and inactive) with: name, username, role, active status, created date.

**FR-USR-002 — Create user**
The system shall allow the Owner to create a user with: name, username, password, and role (`owner`, `kurir`, or `kasir`).

**FR-USR-003 — Edit user**
The system shall allow the Owner to edit a user's name, username, and role. Password can be reset by providing a new value (optional on edit — blank = keep current).

**FR-USR-004 — Deactivate / reactivate user**
Soft-delete via `is_active = false`. A deactivated user cannot log in. Reactivation via `PUT /api/users/{id}`.

**FR-USR-005 — Prevent self-deactivation**
The deactivate action is hidden/disabled for the currently logged-in user.

**FR-USR-006 — Username uniqueness**
The system shall reject a username already taken by any user (active or inactive).

### 5.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-USR-001 | `name` | Required, max 100 chars | `"Nama wajib diisi."` / `"Nama tidak boleh lebih dari 100 karakter."` |
| VAL-USR-002 | `username` | Required, max 50 chars, alphanumeric + underscore | `"Username wajib diisi."` / `"Username tidak boleh lebih dari 50 karakter."` / `"Username hanya boleh berisi huruf, angka, dan garis bawah."` |
| VAL-USR-003 | `username` | Unique | `"Username ini sudah digunakan."` |
| VAL-USR-004 | `password` | Required on create, min 8 chars | `"Kata sandi wajib diisi."` / `"Kata sandi minimal 8 karakter."` |
| VAL-USR-005 | `role` | Required, must be `owner`, `kurir`, or `kasir` | `"Peran wajib dipilih."` |

### 5.4 UI Behavior

- **List page:** Table — Nama, Username, Peran, Status (Aktif/Tidak Aktif badge), Dibuat, Aksi (Edit, Nonaktifkan/Aktifkan)
- **Create/Edit:** Modal form
- **Deactivate:** Dialog — `"Nonaktifkan [nama]? Mereka tidak akan bisa login lagi."`
- **Empty state:** `"Belum ada pengguna. Buat pengguna pertama untuk memulai."`
- **Inactive users:** Muted styling; badge "Tidak Aktif"
- **Notifikasi Toast:** Berhasil buat → `"Pengguna berhasil dibuat."` · Berhasil ubah → `"Pengguna berhasil diperbarui."` · Nonaktifkan → `"Pengguna berhasil dinonaktifkan."` · Aktifkan kembali → `"Pengguna berhasil diaktifkan kembali."` · Gagal → `"Terjadi kesalahan. Silakan coba lagi."`

---

## 6. Module: Location Management (FR-LOC)

**Accessible by:** Owner (full); Kurir (read own vehicle)

### 6.1 User Stories

- As an Owner, I want to register warehouse and each truck as locations so stock can be tracked per location.
- As an Owner, I want to assign a truck to a specific kurir.
- As a Kurir, I want to see my assigned truck so I know which vehicle's stock I'm working with.

### 6.2 Functional Requirements

**FR-LOC-001 — Location list**
The system shall display all locations with: name, type (Gudang/Kendaraan), assigned kurir (for vehicles), active status.

**FR-LOC-002 — Create location**
The system shall allow the Owner to create a location with: name, type (`warehouse` or `vehicle`), and assigned user (required if type is `vehicle`; must be an active owner or kurir).

**FR-LOC-003 — Edit location**
The system shall allow the Owner to edit name and assigned kurir. Type cannot be changed after creation.

**FR-LOC-004 — Deactivate location**
Soft-delete vehicles that are no longer in use. The warehouse location cannot be deactivated. Active vehicles are required for truck loading (FR-STK-005).

**FR-LOC-005 — Kurir sees own vehicle**
A kurir can read their own assigned vehicle location. They cannot see other vehicles or warehouse details.

### 6.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-LOC-001 | `name` | Required, max 100 chars | `"Nama lokasi wajib diisi."` |
| VAL-LOC-002 | `type` | Required, `warehouse` or `vehicle` | `"Tipe lokasi wajib dipilih."` |
| VAL-LOC-003 | `assigned_to` | Required when `type = vehicle`, must be an active user (owner or kurir) | `"Pengguna wajib dipilih untuk kendaraan."` |

### 6.4 UI Behavior

- **List page:** Table — Nama, Tipe (badge: Gudang/Kendaraan), Kurir, Status, Aksi
- **Create/Edit:** Modal form; assigned kurir dropdown only appears when type = Kendaraan
- **Deactivate:** Dialog — `"Nonaktifkan [nama]? Kendaraan ini tidak akan bisa digunakan untuk memuat barang."`
- **Empty state:** `"Belum ada lokasi. Tambahkan gudang atau kendaraan."`
- **Notifikasi Toast:** Berhasil buat → `"Lokasi berhasil dibuat."` · Berhasil ubah → `"Lokasi berhasil diperbarui."` · Nonaktifkan → `"Lokasi berhasil dinonaktifkan."` · Gagal → `"Terjadi kesalahan. Silakan coba lagi."`

---

## 7. Module: Product Management (FR-PRD)

**Accessible by:** Owner (write); all roles (read — used in transaction and stock forms)

### 7.1 User Stories

- As an Owner, I want to add products with the correct category (simple or refillable) so stock tracking is accurate.
- As an Owner, I want to edit product details when pricing or naming changes.
- As an Owner, I want to deactivate a product so it no longer appears in forms for staff.
- As Kurir/Kasir, I want to see only active products when creating a transaction.

### 7.2 Functional Requirements

**FR-PRD-001 — Product list**
The system shall display all products with: name, category, production type, type (air/gas), unit, base price, active status.

**FR-PRD-002 — Create product**
The system shall allow the Owner to create a product with: name, category (`simple` / `refillable`), production_type (`purchased` / `selfproduced`, only for `refillable`), type (`air` / `gas`), unit (selected from controlled vocabulary — see VAL-PRD-005), and base price.

**FR-PRD-003 — Edit product**
The system shall allow the Owner to edit any product field. Editing base price does not affect historical transaction records (price is snapshotted).

**FR-PRD-004 — Deactivate product**
Soft-delete (`is_active = false`). Inactive products are hidden from staff forms but remain in Owner's management list.

**FR-PRD-005 — Product pricing in transactions**
Effective unit price = `CustomerPricing.custom_price` (if exists for the customer) otherwise `Products.base_price`.

### 7.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-PRD-001 | `name` | Required, max 100 chars | `"Nama produk wajib diisi."` / `"Nama produk tidak boleh lebih dari 100 karakter."` |
| VAL-PRD-002 | `category` | Required, `simple` or `refillable` | `"Kategori produk wajib dipilih."` |
| VAL-PRD-003 | `production_type` | Required when `category = refillable`; `purchased` or `selfproduced` | `"Tipe produksi wajib dipilih untuk produk refillable."` |
| VAL-PRD-004 | `type` | Required, `air` or `gas` | `"Jenis produk wajib dipilih."` |
| VAL-PRD-005 | `unit` | Required; must be one of the controlled vocabulary values: `galon`, `tabung 3kg`, `tabung 12kg`, `karton`, `dus`, `cup`, `botol`, `pcs` | `"Satuan wajib dipilih."` / `"Satuan tidak valid."` |
| VAL-PRD-006 | `base_price` | Required, positive, max 15 digits with 2 dp | `"Harga dasar wajib diisi."` / `"Harga dasar harus berupa angka positif."` |

### 7.4 UI Behavior

- **List page:** Table — Nama, Kategori (badge: Sederhana/Refillable), Tipe (Air/Gas), Satuan, Harga Dasar, Status, Aksi
- **Create/Edit:** Modal form; production type field appears only when category = Refillable
- **Deactivate:** Dialog — `"Nonaktifkan [nama]? Produk tidak akan muncul di formulir baru."`
- **Empty state:** `"Belum ada produk. Tambahkan produk air atau gas pertama Anda."`
- **Notifikasi Toast:** Berhasil buat → `"Produk berhasil dibuat."` · Berhasil ubah → `"Produk berhasil diperbarui."` · Nonaktifkan → `"Produk berhasil dinonaktifkan."` · Gagal → `"Terjadi kesalahan. Silakan coba lagi."`

---

## 8. Module: Customer Management (FR-CST)

**Accessible by:** Owner (write, pricing); all roles (read — used in transaction form)

### 8.1 User Stories

- As any staff, I want to register customers with name, phone, and address.
- As an Owner, I want to set custom prices per product for specific customers.
- As any staff, I want to deactivate inactive customers.
- As Kurir/Kasir, I want to select a customer when creating a transaction.

### 8.2 Functional Requirements

**FR-CST-001 — Customer list**
Display all customers with: name, phone, address (truncated), active status.

**FR-CST-002 — Create customer**
Owner creates customer with: name, phone (optional), address (optional).

**FR-CST-003 — Edit customer**
Owner edits name, phone, address.

**FR-CST-004 — Deactivate customer**
Soft-delete. Inactive customers hidden from staff's transaction picker.

**FR-CST-005 — Custom pricing**
Via `GET /PUT /api/customers/{id}/pricing`. Custom price overrides base price when that customer is selected in a transaction.

**FR-CST-006 — Custom pricing display**
Pricing UI shows all active products with: product name, base price, optional custom price input (blank = use base price).

**FR-CST-007 — Pricing resolution**
`CustomerPricing.custom_price` (if exists) → `Products.base_price`. Resolved at API when transaction form loads. Stored as `TransactionItems.unit_price` at creation.

**FR-CST-008 — Konfidensial Pelanggan**
Owner dapat menandai pelanggan sebagai **konfidensial** melalui checkbox "Konfidensial" di form buat/edit pelanggan. Pelanggan konfidensial:
- Hanya terlihat oleh role `owner` di halaman Pelanggan dan di pemilih pelanggan Langkah 1 `TransactionsPage`.
- Tidak muncul dalam respons `GET /api/customers` untuk role kasir dan kurir (filter server-side).
- Mendapatkan badge "Konfidensial" (pink) pada kartu pelanggan di tampilan owner.
- Tetap dapat diakses melalui endpoint owner-only (`/debt-history`, `/pricing`, `/container-loans`, dll.) tanpa perubahan.
- Kasir tidak dapat mengubah status konfidensial meskipun mengakses endpoint PUT — nilai `is_confidential` diabaikan untuk non-owner.

### 8.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-CST-001 | `name` | Required, max 100 chars | `"Nama pelanggan wajib diisi."` / `"Nama tidak boleh lebih dari 100 karakter."` |
| VAL-CST-002 | `phone` | Optional; if provided: max 20 chars, digits/spaces/+/- only | `"Nomor telepon tidak boleh lebih dari 20 karakter."` / `"Nomor telepon mengandung karakter yang tidak valid."` |
| VAL-CST-003 | `address` | Optional, max 500 chars | `"Alamat tidak boleh lebih dari 500 karakter."` |
| VAL-CST-004 | `custom_price` | Optional; if provided: positive, max 15 digits with 2 dp | `"Harga khusus harus berupa angka positif."` |

### 8.4 UI Behavior

- **List page:** Table — Nama, Telepon, Alamat, Status, Aksi (Edit, Harga Khusus, Nonaktifkan)
- **Pricing page:** Sub-page — satu baris per produk aktif dengan input harga khusus opsional
- **Deactivate:** Dialog — `"Nonaktifkan [nama]? Mereka tidak akan muncul di transaksi baru."`
- **Empty state (list):** `"Belum ada pelanggan. Tambahkan pelanggan pertama Anda."`
- **Empty state (pricing):** `"Tidak ada produk aktif untuk dikonfigurasi harganya."`
- **Notifikasi Toast:** Berhasil buat → `"Pelanggan berhasil dibuat."` · Berhasil ubah → `"Pelanggan berhasil diperbarui."` · Nonaktifkan → `"Pelanggan berhasil dinonaktifkan."` · Simpan harga khusus → `"Harga khusus berhasil disimpan."` · Gagal → `"Terjadi kesalahan. Silakan coba lagi."`

---

## 9. Module: Stock Management (FR-STK)

**Accessible by:** Owner (full); Kurir (levels, movements, transfer, vendor exchange); Kasir (levels — read-only)

### 9.1 User Stories

- As an Owner, I want to receive stock into the warehouse (from vendor or own production) with filled/empty count.
- As an Owner, I want to load a kurir's truck from warehouse stock before their departure.
- As a Kurir, I want to record my truck return: empties brought back, unsold filled items returned to warehouse.
- As an Owner, I want to write off defective items (leaking gas, damaged jug) with a note.
- As an Owner, I want to see current stock per location (warehouse and each truck), split by filled/empty for refillable products, with a summary row showing total empty containers pooled by unit.
- As a Kurir, I want to initiate a vendor exchange (take empties to vendor, receive filled stock back) from the stock page.
- As a Kurir, I want to view stock movement history so I can verify my own transfers.
- As a Kasir, I want to view current warehouse stock levels (read-only) to answer customer inquiries.
- As an Owner, I want to view the full stock movement history to audit changes.
- As an Owner/Kurir, I want to record a vendor exchange (take empties to vendor, receive filled stock back) and record the purchase cost paid.
- As an Owner, I want to record an in-house production refill: empty containers are filled, atomically decrementing empty and incrementing filled count.

### 9.2 Functional Requirements

**FR-STK-001 — Computed stock per location**
Stock is computed per product per location:
- For `simple` products: `SUM(quantity IN to location) - SUM(quantity OUT of location)`
- For `refillable` products: computed separately for `container_status = 'filled'` and `container_status = 'empty'`

There is no stored stock column — this avoids sync issues.

**FR-STK-002 — Stock level polling**
The stock levels page polls `GET /api/stock/levels` every **5 seconds** when active. Polling pauses when the browser tab is hidden.

**FR-STK-003 — Stock level display**
Shows one row per product per location: name, unit, filled count (refillable), empty count (refillable), total (simple). Products at zero or negative are highlighted with a warning badge.

Additionally, for each location, a dashed summary row is displayed for each unit that has empty refillable containers:
> **Pool Kosong — {unit}: {total}** (aggregates empty counts across all products sharing the same unit, e.g. Galon Aqua + Galon Vit + Galon Isi Ulang all count toward "Pool Kosong — galon")

This reflects that empty containers of the same unit type are physically interchangeable (cross-brand exchange).

**FR-STK-004 — Receive stock**
Owner records stock received into any active location (warehouse or vehicle):
- `movement_type = 'receive'`, `to_location = any active location`, `from_location = null`
- For `refillable` products: specify `container_status` (`filled` or `empty`) and quantity
- For `simple` products: specify quantity only (`container_status = 'na'`)
- **Does NOT deduct empty containers.** For in-house refill of `selfproduced` products, use FR-STK-011 (production).
- **Multi-product batch:** The system shall allow recording multiple products in a single receive operation. Destination location and optional batch note are shared; each item specifies its own product, container status, quantity, and optional purchase cost. The frontend submits all items via `POST /api/stock/movements/bulk`.

**FR-STK-005 — Truck loading (warehouse → vehicle)**
Before a kurir departs:
- `movement_type = 'transfer'`, `from_location = warehouse`, `to_location = kurir vehicle`
- For `refillable`: specify container status (`filled` or `empty`) — empty containers may be loaded for a vendor exchange run
- This deducts stock from warehouse and adds it to the vehicle's stock
- **Multi-product batch:** The system shall allow selecting multiple products in a single truck loading submission. The from/to location pair and optional note are shared across all items; each item specifies its own product, container status, and quantity. The frontend submits via `POST /api/stock/transfer/bulk`.

**FR-STK-006 — Truck return (vehicle → warehouse)**
When a kurir returns:
- `movement_type = 'transfer'`, `from_location = vehicle`, `to_location = warehouse`
- For `refillable`: specify separate quantities for `filled` (unsold items returned) and `empty` (containers collected from customers) — add separate item rows per container status
- **Multi-product batch:** Same bulk submission as FR-STK-005 — multiple product rows with the same shared from/to location. The frontend submits via `POST /api/stock/transfer/bulk`.

**FR-STK-007 — Defect write-off**
Owner records a defective item:
- `movement_type = 'defect'`, `from_location = warehouse (or vehicle)`, `to_location = null`
- `note` is mandatory — must describe the defect
- For `refillable`: specify `container_status` of the defective item

**FR-STK-008 — Stock movement history**
Paginated list showing: date/time, product, movement type, container status, quantity, from/to location, note, recorded by.

**FR-STK-009 — Negative stock soft warning**
On stock-out/dispatch/transfer-out: if quantity would result in stock going below zero, display `"Peringatan: Ini akan membuat stok [nama produk] menjadi di bawah nol."` Submission is not blocked.

**FR-STK-010 — Vendor exchange**
Owner **or Kurir** records a vendor exchange (taking empties to vendor, receiving filled stock):
- The API endpoint `POST /api/stock/vendor-exchange` accepts: `location_id` (source location), `product_id`, `qty_empty_out` (empties handed to vendor), `qty_filled_in` (filled received back), `purchase_cost` (amount paid to vendor, required)
- The server atomically creates two `StockMovements` records:
  1. `movement_type = 'transfer'`, `from_location = location_id`, `to_location = null`, `container_status = 'empty'`, `quantity = qty_empty_out` (empties sent to vendor)
  2. `movement_type = 'receive'`, `from_location = null`, `to_location = location_id`, `container_status = 'filled'`, `quantity = qty_filled_in`, `purchase_cost = purchase_cost`
- No new DB enum value — vendor exchange is purely a UI/API concept

**FR-STK-011 — In-house production refill**
Owner records an in-house refill for `selfproduced` refillable products (e.g. filling Galon Isi Ulang from the store's own water source):
- Endpoint: `POST /api/stock/production`
- Accepts: `product_id` (must be `refillable` + `selfproduced`), `location_id`, `quantity`, optional `production_cost`, optional `notes`
- Atomically: decrements `quantity_empty` by `quantity`; increments `quantity_filled` by `quantity`
- Creates a `StockMovement` record with `movement_type = 'production'`, `from_location = location_id`, `to_location = location_id`
- Available to Owner and Kasir

**FR-STK-012 — Date filter for stock movement history**
The movement history shall be filtered by date. `GET /api/stock/movements` accepts an optional `date` query param (YYYY-MM-DD, interpreted in WIB timezone). When provided, only movements whose `created_at` falls on that date in WIB are returned. When omitted, the API defaults to today WIB. The frontend filter defaults to today WIB and is lazy-loaded the first time the Riwayat tab is opened.

**FR-STK-013 — Bulk vendor exchange (multi-product)**
The Vendor Exchange tab shall support submitting multiple products in a single operation, mirroring the bulk pattern of FR-STK-004 (Receive) and FR-STK-005/006 (Transfer). The form layout is:
1. **Shared location** — a single location picker (truk / gudang) at the top of the form, applying to all items
2. **Per-item product rows** — each row contains: Product selector, Jumlah Kosong Diserahkan, Jumlah Terisi Diterima, Biaya Pembelian (Rp). Rows can be added with "+ Tambah Produk" and removed (remove button is disabled when only one row remains).
3. **Shared catatan** (optional) at the bottom, applying to all items
4. **Total Biaya Pembelian** — a read-only computed sum row showing the aggregate purchase cost across all items
5. A single **"Simpan Pertukaran"** button that submits all items at once via `POST /api/stock/vendor-exchange/bulk`

The server creates two `StockMovements` records per item atomically (same as single-product `POST /api/stock/vendor-exchange`).

**FR-STK-015 — Negative stock warning for Transfer tab**
Before submitting a transfer, the frontend checks each item's requested quantity against the current stock level at the source location (`from_location_id`). For simple products, `quantity_total` is used; for refillable products, `quantity_filled` (if `container_status = filled`) or `quantity_empty` (if `container_status = empty`) is used. If any item would result in negative stock (available − requested < 0), a `ConfirmDialog` is displayed listing the affected products with their available and requested quantities. The user may confirm to proceed anyway (soft warning — the API does not enforce a minimum) or cancel to revise the form. The check runs on every submit attempt and uses the `levels` data already loaded on the Stock page.

**FR-STK-016 — Auto-populate Transfer tab from vehicle stock**
When the user selects a vehicle location as the "Dari Lokasi" in the Transfer tab, the item list is automatically pre-filled with all products currently stocked on that vehicle, using the `levels` data already loaded on page mount. Population rules:
- `simple` product with `quantity_total > 0` → one row with `container_status = na` and `quantity = quantity_total`
- `refillable` product with `quantity_filled > 0` → one row with `container_status = filled` and `quantity = quantity_filled`
- `refillable` product with `quantity_empty > 0` → one row with `container_status = empty` and `quantity = quantity_empty`

If the vehicle has no stock, the list resets to a single blank row (no banner). When the user switches the source location to a warehouse or clears it, the list also resets to a single blank row. When switching between two vehicle locations, the list re-populates with the newly selected vehicle’s stock. The user can still edit quantities, remove rows, or add rows after auto-population. The negative-stock `ConfirmDialog` (FR-STK-015) continues to apply normally after auto-population. No new API endpoints are required — the feature reads from `levels` state already maintained by the Stock page.

### 9.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-STK-001 | `product_id` | Required, active product | `"Produk wajib dipilih."` |
| VAL-STK-002 | `movement_type` | Required, valid enum | `"Tipe pergerakan wajib dipilih."` |
| VAL-STK-003 | `container_status` | Required `filled`/`empty` for refillable; must be `na` for simple | `"Status kontainer wajib dipilih untuk produk refillable."` |
| VAL-STK-004 | `quantity` | Required, positive integer, min 1 | `"Jumlah wajib diisi."` / `"Jumlah minimal 1."` |
| VAL-STK-005 | `to_location_id` | Required for `receive`; for `transfer`: required unless it is a vendor handover (`to_location` may be null when sending empties to external vendor) | `"Lokasi tujuan wajib dipilih."` |
| VAL-STK-006 | `from_location_id` | Required for `transfer`, `dispatch`, `defect` | `"Lokasi asal wajib dipilih."` |
| VAL-STK-007 | `note` | Required for `defect`; optional otherwise, max 255 chars | `"Catatan wajib diisi untuk barang cacat."` / `"Catatan tidak boleh lebih dari 255 karakter."` |
| VAL-STK-008 | `purchase_cost` | Required for vendor exchange (`POST /api/stock/vendor-exchange`); must be a positive decimal | `"Biaya pembelian wajib diisi untuk pertukaran vendor."` / `"Biaya pembelian harus berupa angka positif."` |
| VAL-STK-009 | `product_id` (production) | Must be a `refillable` product with `production_type = 'selfproduced'` | `"Produksi hanya berlaku untuk produk isi ulang produksi sendiri."` |
| VAL-STK-010 | `quantity` (production) | Required, positive integer, min 1 | `"Jumlah produksi wajib diisi dan minimal 1."` |

### 9.4 UI Behavior

- **Stock levels page:** Cards per location (Gudang + each truck) — one card per product showing: filled count + empty count (refillable) or total (simple); dashed Pool Kosong summary row per unit with empty aggregation; polling every 5s; `"Terakhir diperbarui: X detik yang lalu"`
- **Tab visibility by role:** Owner sees all tabs (Level Stok, Riwayat, Terima Stok, Tukar Agent, Produksi, Transfer, Defek/Rusak, **Kontainer**). Kurir sees: Level Stok, Riwayat, Transfer, Tukar Agent. Kasir sees: Level Stok only (read-only).
- **Receive form:** Shared location picker at top → one or more item rows (each: product, container status, quantity, optional purchase cost) → "+ Tambah Produk" button to add rows → shared note → "Terima Semua Stok" button. Minimum 1 row; remove button disabled on the last row. Calls `POST /api/stock/movements/bulk`.
- **Transfer form (loading / return):** Shared from/to location pickers at top → one or more item rows (each: product, container status, quantity) → "+ Tambah Produk" button → shared note → "Transfer Stok" button. Tab subtitle reads: *"Pindahkan stok antar lokasi. Gunakan untuk muat truk (gudang → kendaraan) atau barang kembali (kendaraan → gudang)."* Calls `POST /api/stock/transfer/bulk`.
- **Defect form:** Product picker → location picker → container status → quantity → mandatory note
- **Vendor exchange form (FR-STK-013):** Shared location picker at top → one or more item rows (each: product, Jumlah Kosong Diserahkan, Jumlah Terisi Diterima, Biaya Pembelian) → "+ Tambah Produk" button → shared note → Total Biaya Pembelian computed row → "Simpan Pertukaran" button. Calls `POST /api/stock/vendor-exchange/bulk`.
- **Movement history:** Date filter at top of Riwayat tab (same pattern as FR-DSH-006): `<input type="date">` + conditional "Hari Ini" button; defaults to today WIB; calling `GET /api/stock/movements?date=YYYY-MM-DD`. Movements are lazy-loaded when the tab is first opened (and reloaded when date changes). Newest first; movement type badge (Terima/Transfer/Cacat); container status badge (Isi/Kosong/N/A). Purchase cost is hidden for `vendor_exchange` movements when the role is Kurir or Kasir (purchase cost is owner-only business information; cost from `dispatch` movements — i.e. selling transactions — remains visible to all roles).
- **Empty state (levels):** `"Tidak ada produk. Tambahkan produk untuk melacak stok."`
- **Empty state (history):** `"Belum ada pergerakan stok tercatat."`
- **Notifikasi Toast:** Terima stok → `"Stok berhasil diterima."` · Transfer → `"Transfer stok berhasil."` · Defek → `"Defek berhasil dicatat."` · Tukar agent → `"Tukar agent berhasil dicatat."` · Produksi → `"Produksi berhasil dicatat."` · Kembalian kontainer → `"Pengembalian berhasil dicatat."` · Gagal → `"Gagal menyimpan. Periksa kembali data."`

---

## 10. Module: Transactions (FR-TXN)

**Accessible by:** Kurir (delivery transactions, view own); Kasir (counter, view own); Owner (view all, cancel any, create any type)

### 10.1 User Stories

- As a Kurir, I want to create a delivery transaction from my truck stock, recording which customer and what was delivered.
- As a Kasir, I want to create a counter sale from warehouse stock when a customer walks in.
- As a Kasir/Owner, I want to create a sale without selecting a customer for anonymous walk-in purchases.
- As any staff, I want to record partial payment and leave the remainder as customer debt.
- As any staff, I want to optionally collect an old customer debt payment at the same time as a new transaction.
- As any staff, I want to record empty containers returned by the customer at the time of delivery.
- As a Kurir, I want to cancel my own transaction if something went wrong.
- As an Owner, I want to see all transactions from all staff.
- As an Owner, I want to cancel any transaction, knowing stock will be automatically reversed.

### 10.2 Functional Requirements

**FR-TXN-001 — Create transaction (3-step flow)**
All transaction types use the same 3-step flow:

1. **Langkah 1 — Pilih Pelanggan:**
   - Kurir: customer selection is **required** (delivery always has an identifiable customer)
   - Kasir / Owner: customer selection is **optional** — a "Lewati (Tanpa Pelanggan)" button allows skipping for anonymous walk-in sales
   - Searchable list by name and phone number; outstanding debt shown on each customer row

2. **Langkah 2 — Pilih Produk:**
   - Tipe Transaksi and Lokasi Stok are **locked and auto-filled** based on the user's role:
     - Kasir: `type = counter`, `location = warehouse` (read-only, cannot be changed)
     - Kurir: `type = delivery`, `location = their assigned vehicle` (read-only, cannot be changed)
     - Owner: can select `counter` or `delivery`; location selected via dropdown
   - Products listed with inline quantity control: − button, direct keyboard-editable number input, + button
   - Running total shown at bottom
   - Warning banner if refillable products are added without a customer: `"Produk refillable dipilih tanpa pelanggan — pinjaman kontainer tidak akan dicatat."`

3. **Langkah 3 — Tinjau & Kirim:**
   - Order summary card; shows "Tanpa Pelanggan" if no customer selected
   - If customer has outstanding debt: **red highlighted block** showing "Total Hutang Pelanggan: Rp X" above the payment field
   - Payment method selector (default: Tunai)
   - Jumlah Dibayar input (default: transaction total, editable downward to 0)
   - If customer has outstanding debt: optional **"Bayar Hutang Lama (Rp)"** field — when filled, creates a separate `DebtPayments` record on submit alongside the transaction
   - If any refillable product in cart AND customer selected: **"Kembalian Kontainer (opsional)"** section — one input per refillable product: "Jumlah kontainer kosong dikembalikan — [ProductName]"
   - Sisa utang shown automatically below Jumlah Dibayar
   - Notes textarea (optional)
   - "Kirim Transaksi" bottom-anchored

On submit, the system shall:
- Create a `Transactions` record with `status = 'completed'`, `staff_id = current user`, `transaction_type` based on context
- Create `TransactionItems` records with `unit_price` snapshotted at creation time
- Create `StockMovements` records with `movement_type = 'dispatch'` for each item (deducts `quantity_filled` from source location for refillable; deducts `quantity_total` for simple)
- Create a `Payments` record with `paid_amount` (if > 0)
- For `refillable` products, if customer selected: create `ContainerLoans` records (positive quantity = containers lent to customer)
- For container returns entered in Step 3: create `ContainerLoans` records (negative quantity); create `StockMovements` records (`movement_type = 'receive'`, `container_status = 'empty'`, at source location)
- If `debt_payment_amount > 0`: create a `DebtPayments` record for the customer

**FR-TXN-002 — Transaction types and stock source**

| Type | Created by | Stock source | Stock deducted? |
|---|---|---|---|
| `delivery` | Owner / Kurir | Vehicle (kurir: assigned vehicle; owner: any active vehicle) | Yes — `dispatch` from vehicle |
| `counter` | Owner / Kasir | Warehouse | Yes — `dispatch` from warehouse |

**FR-TXN-003 — Price snapshot**
`TransactionItems.unit_price` is immutable after creation. Effective unit price = custom price for the customer (if any) or product base price.

**FR-TXN-004 — Total and debt amount**
- `total_amount = SUM(quantity × unit_price)`
- `debt_amount = total_amount - paid_amount` (computed, stored for query performance)
- `paid_amount` may be 0 (full debt), partial, or equal to `total_amount` (fully paid)

**FR-TXN-005 — Payment method**
Staff selects payment method: `cash`, `transfer`, or `qris`. In Phase 1, `qris` is recorded manually.

**FR-TXN-006 — Transaction list — Kurir/Kasir**
Staff sees only their own transactions, filtered by the selected date (see FR-TXN-015). List shows: tanggal, pelanggan (or "Tanpa Pelanggan"), total, lunas, sisa utang, status badge, metode bayar.

**FR-TXN-007 — Transaction list — Owner**
Owner sees all transactions, filtered by the selected date (see FR-TXN-015). List shows: tanggal, pelanggan, staff, tipe transaksi, total, lunas, sisa utang, status badge.

**FR-TXN-008 — Transaction detail**
Shows: customer info (or "Tanpa Pelanggan"), staff name, transaction type, all items (product, qty, unit price, subtotal), total, paid amount, debt amount, payment method, status, notes, container returns recorded.

**FR-TXN-009 — Cancel transaction**
Any role can cancel their own transaction. Owner can cancel any transaction. On cancellation:
- System creates compensating `StockMovements` records (`movement_type = 'receive'`, back to the original source location) for each item — restoring stock to source location
- System creates compensating `ContainerLoans` records (negative quantity) to reverse any loans recorded at creation
- Container returns recorded at creation are also reversed (positive ContainerLoans, negative StockMovements for empty returns)
- `status` is set to `'cancelled'`

**FR-TXN-010 — No edit transaction**
Transactions cannot be edited after creation. To correct a mistake, cancel the transaction and create a new one.

**FR-TXN-011 — Refillable product stock handling**
Selling a refillable product creates a `dispatch` StockMovement (`quantity_filled -= qty` at source location) and, if a customer is selected, a `ContainerLoans` record (positive qty = containers lent).

Container return recorded in Step 3 (per refillable item):
- Creates a `receive` StockMovement: `container_status = 'empty'`, `quantity_empty += returned_qty` at source location
- Creates a `ContainerLoans` record (negative qty) if customer is selected

**FR-TXN-012 — Anonymous sale (no customer)**
Kasir and Owner may create a transaction without selecting a customer (`customer_id = null`). In this case:
- No `ContainerLoans` records are created (no customer to track against)
- A warning banner is shown in Step 2 if refillable products are in the cart

**FR-TXN-013 — Status model**
All transactions are created with `status = 'completed'`. There is no `pending` state. A transaction's status can only change to `'cancelled'` via the cancel action (FR-TXN-009).

**FR-TXN-014 — Minimum items**
A transaction must have at least one item. Submit button disabled until one product is added.

**FR-TXN-015 — Date filter for transaction list and assignment list**
The transaction list and assignment list share a single date filter. `GET /api/transactions` accepts an optional `date` query param (YYYY-MM-DD, interpreted in WIB timezone). When provided, only transactions whose `created_at` falls on that date in WIB are returned. When omitted, the API defaults to today WIB. `GET /api/assignments` accepts the same optional `date` query param; when provided, only assignments whose `created_at` falls on that date in WIB are returned. The frontend uses a single `selectedDate` state defaulting to today WIB; the date filter row is always visible above the status tabs regardless of the active tab.

**FR-TXN-016 — Delivery Assignment creation (Owner/Kasir)**
Owner and Kasir can create a delivery assignment for a Kurir. An assignment is a pure task record — it has zero stock side effects. No stock is reserved or deducted at creation time.
- Endpoint: `POST /api/assignments`
- Body: `{ kurir_id, customer_id, location_id, items: [{ product_id, quantity, unit_price }], notes? }`
- `location_id` is required and must be an active location (warehouse or vehicle). It represents the stock source from which the Kurir will dispatch goods when fulfilling the assignment.
- Server creates a `DeliveryAssignment` record with `status = 'pending'` and stores the chosen `location_id`.
- The UI uses a 2-step overlay: **Step 1** = select Kurir + Customer + Location (any active warehouse or vehicle, shown as a button list); **Step 2** = select products + notes. The "Lanjut" button is disabled until all three are selected.

**FR-TXN-017 — Assignment list**
All roles can view the Penugasan tab on the Transactions page.
- Endpoint: `GET /api/assignments?date=YYYY-MM-DD` (date param shared with transaction date filter; see FR-TXN-015)
- Owner/Kasir: all assignments. Kurir: only assignments where `kurir_id = currentUser.id`
- List shows: status badge, date, customer name, kurir name, items summary, notes
- Kurir's default tab on the Transactions page is Penugasan

**FR-TXN-018 — Assignment fulfillment (Kurir)**
Kurir can process a pending assignment by pressing "Proses". This opens the standard transaction overlay pre-populated with the assignment's customer, items, and stock location. The **Lokasi Stok** field is pre-filled from `assignment.location_id` and locked (read-only). On confirm, the system calls `POST /api/assignments/{id}/fulfill` which:
1. Uses the stored `location_id` on the assignment as the stock source (falls back to the kurir's assigned vehicle for legacy assignments that predate this field)
2. Creates a `delivery` transaction dispatching stock based on the actual delivered items (falls back to the assignment's original items if none supplied)
3. Marks the assignment `status = 'fulfilled'` and stores `transaction_id`

The kurir may adjust item quantities in Step 2 before confirming (e.g., partial delivery). If the delivered items differ from the assignment (quantity changed, product added or removed), the **Notes field becomes mandatory** and the submit is blocked with a toast until notes are filled in. Stock is deducted and the transaction total is calculated from the actual delivered quantities, not the assignment's planned quantities.

**FR-TXN-019 — Assignment cancellation (Owner/Kasir)**
Owner and Kasir can cancel a pending assignment via a confirm dialog. No stock effects.
- Endpoint: `PUT /api/assignments/{id}/cancel`
- Sets `status = 'cancelled'`

**FR-TXN-020 — Assignment status model**
`DeliveryAssignmentStatus` has three values: `pending`, `fulfilled`, `cancelled`. Only `pending` assignments can be processed or cancelled.

**FR-TXN-021 — Negative stock warning on transaction creation**
Before submitting in Langkah 3, the frontend checks each cart item's quantity against the current stock level at the selected `location_id`. For refillable products, `quantity_filled` is used (transactions always dispatch filled stock); for simple products, `quantity_total` is used. If any item would result in negative stock (available − quantity < 0), a `ConfirmDialog` is shown with the list of affected products. The user may confirm to proceed or cancel. This warning applies to both regular transaction creation and Kurir fulfillment of a delivery assignment. Stock levels are loaded in the page's `load()` function alongside other data.

### 10.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-TXN-001 | `customer_id` | Required for Kurir; optional for Kasir/Owner | `"Pelanggan wajib dipilih."` (kurir only) |
| VAL-TXN-002 | `items` | At least one item | `"Tambahkan minimal satu produk."` |
| VAL-TXN-003 | `items[].product_id` | Active product | `"Produk yang dipilih tidak valid."` |
| VAL-TXN-004 | `items[].quantity` | Required, positive integer, min 1 | `"Jumlah minimal 1."` |
| VAL-TXN-005 | `payment_method` | Required, `cash`/`transfer`/`qris` | `"Metode pembayaran wajib dipilih."` |
| VAL-TXN-006 | `paid_amount` | Required, 0 ≤ paid_amount ≤ total_amount | `"Jumlah bayar tidak boleh melebihi total transaksi."` |
| VAL-TXN-007 | `notes` | Optional, max 500 chars | `"Catatan tidak boleh lebih dari 500 karakter."` |
| VAL-TXN-008 | `transaction_type` | Must be valid per role: kurir → `delivery` only; kasir → `counter` only | `"Tipe transaksi tidak valid untuk peran Anda."` |
| VAL-TXN-009 | `container_returns[].quantity` | Non-negative integer | `"Jumlah kembalian kontainer tidak boleh negatif."` |
| VAL-TXN-010 | `debt_payment_amount` | Optional, non-negative; only applicable if customer has outstanding debt | `"Jumlah pembayaran hutang tidak valid."` |

### 10.4 UI Behavior

- **Transaction form (all roles — mobile-first):**
  - Step indicators at top (1 → 2 → 3)
  - Langkah 1: Full-screen searchable customer list (min 44px row height); debt indicator on each customer row; "Lewati (Tanpa Pelanggan)" button at footer for Kasir/Owner only
  - Langkah 2: Tipe Transaksi and Lokasi Stok shown as read-only labels for Kasir/Kurir; Owner sees dropdowns (counter/delivery only); active product list with −/input/+ quantity control (input is directly editable via keyboard for large quantities); running total at bottom; yellow refillable warning banner shown if no customer; tombol "Lanjut" bottom-anchored
  - Langkah 3: Summary card (shows "Tanpa Pelanggan" if no customer), payment method selector (3 large buttons, default = **Tunai/Cash**), red debt box if customer has outstanding debt, Jumlah Dibayar input (default = total, editable), optional "Bayar Hutang Lama" input (visible only if customer has debt), "Kembalian Kontainer" section (per refillable product in cart, visible only if customer selected), sisa utang shown automatically, notes textarea, "Kirim Transaksi" button bottom-anchored

- **Transaction list:**
  - Date filter above status tabs: `<input type="date">` defaulting to today WIB + conditional "Hari Ini" shortcut button (hidden when already on today); always visible for all tabs. On transaction tabs calls `GET /api/transactions?date=YYYY-MM-DD`; on Penugasan tab calls `GET /api/assignments?date=YYYY-MM-DD` (see FR-TXN-015).
  - Filter: Semua / Selesai / Dibatalkan / **Penugasan** — Penugasan tab shows assignment cards (see FR-TXN-016–FR-TXN-020); default tab for Kurir is Penugasan
  - Owner: additional filter by transaction type (hidden on Penugasan tab)
  - Status badges: Selesai (hijau), Dibatalkan (abu-abu), Menunggu (kuning for assignments)
  - Debt indicator: show "Ada utang: Rp X" in red on rows with outstanding debt
  - "Batalkan" button visible for any non-cancelled transaction (own for kurir/kasir; any for owner)
  - "Buat Penugasan" button in header (Owner/Kasir only)

- **Cancel confirmation dialog:**
  - For all cancels: `"Batalkan transaksi ini? Stok akan dikembalikan secara otomatis."`

- **QRIS note:** Inline info banner when `method = 'qris'` — `"Integrasi QRIS otomatis hadir di Fase 2. Konfirmasi pembayaran secara manual."`

- **Empty state:** `"Belum ada transaksi."` (kurir/kasir) / `"Belum ada transaksi tercatat."` (owner)
- **Notifikasi Toast:** Buat transaksi → `"Transaksi berhasil dibuat."` · Batalkan transaksi → `"Transaksi berhasil dibatalkan."` · Tambah pembayaran → `"Pembayaran berhasil dicatat."` · Buat penugasan → `"Penugasan berhasil dibuat."` · Batalkan penugasan → `"Penugasan berhasil dibatalkan."` · Selesaikan penugasan → `"Penugasan berhasil diselesaikan."` · Gagal → `"Terjadi kesalahan. Silakan coba lagi."`

---

## 11. Module: Payments & Debt (FR-PAY)

**Accessible by:** Owner (full — record and view all); Kurir/Kasir (view own transactions' payment status)

### 11.1 User Stories

- As an Owner, I want to record additional payment on a partially-paid transaction until it is fully settled.
- As an Owner, I want to record a standalone debt payment from a customer who is settling old debt without a new transaction.
- As an Owner, I want to see the outstanding debt balance per customer.
- As Kurir/Kasir, I want to see the payment status of my own transactions.

### 11.2 Functional Requirements

**FR-PAY-001 — Payment record at transaction creation**
A `Payments` record is created when a transaction is submitted, with `amount = paid_amount`. If `paid_amount = 0`, no Payments record is created.

**FR-PAY-002 — Additional payment on transaction**
Owner can record additional payment(s) via `POST /api/transactions/{id}/payments` until `SUM(Payments.amount) = total_amount`.

**FR-PAY-003 — Transaction payment status**
Derived:
- `SUM = 0` → **Belum Bayar**
- `0 < SUM < total_amount` → **Bayar Sebagian**
- `SUM = total_amount` → **Lunas**

**FR-PAY-004 — Standalone debt settlement (DebtPayments)**
Owner records a lump-sum payment from a customer settling accumulated debt, not tied to a specific transaction. Via `POST /api/debt-payments`.

**FR-PAY-005 — Customer outstanding debt**
Computed: `SUM(Transactions.debt_amount WHERE customer) - SUM(DebtPayments.amount WHERE customer)`. Visible on customer detail page.

**FR-PAY-006 — QRIS Phase 1 limitation**
`qris` is recorded as method only. Note: `"Integrasi gateway QRIS akan hadir di Fase 2. Tandai pembayaran secara manual untuk saat ini."`

### 11.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-PAY-001 | `amount` (additional) | Required, positive, ≤ remaining unpaid | `"Jumlah bayar melebihi sisa tagihan."` |
| VAL-PAY-002 | `method` | Required, `cash`/`transfer`/`qris` | `"Metode pembayaran wajib dipilih."` |
| VAL-PAY-003 | `reference_no` | Optional, max 100 chars | `"Nomor referensi tidak boleh lebih dari 100 karakter."` |
| VAL-PAY-004 | `amount` (debt payment) | Required, positive | `"Jumlah pembayaran wajib diisi dan harus positif."` |
| VAL-PAY-005 | `customer_id` (debt payment) | Required, active customer | `"Pelanggan wajib dipilih."` |

### 11.4 UI Behavior

- **Transaction detail:** Payment section shows paid so far, remaining debt, each payment event
- **"Tambah Pembayaran" button:** Visible to Owner when `SUM(paid) < total_amount`; opens bottom-sheet: amount input (default = remaining), method selector, optional ref number
- **Payment status badges:** Belum Bayar (merah), Bayar Sebagian (oranye), Lunas (hijau)
- **Customer detail — debt section:** Total outstanding; tombol "Catat Pelunasan Utang"
- **Debt payment form:** Customer name (read-only), amount, method, optional ref number, notes
- **QRIS note:** Inline info banner
- **Notifikasi Toast:** Catat pembayaran hutang → `"Pembayaran berhasil dicatat."` · Gagal → `"Terjadi kesalahan. Silakan coba lagi."`

---

## 12. Module: Container Loans (FR-CON)

**Accessible by:** Owner (full); Kurir/Kasir (record returns during transaction — see FR-TXN-001)

### 12.0 Konsep: Semantik Kuantitas

Setiap baris `ContainerLoans` mencatat **satu peristiwa** pertukaran kontainer antara bisnis dan pelanggan:

| `quantity` | Arti | Contoh |
|---|---|---|
| **+N (positif)** | Kita menyerahkan N kontainer **terisi** kepada pelanggan — pelanggan kini memegang aset kita | Antar 10 galon terisi ke Bu Ani |
| **−N (negatif)** | Pelanggan menyerahkan N kontainer **kosong** kepada kita — kita kini memegang kontainer milik pelanggan | Bu Ani mengembalikan 20 galon kosong |

**Net = SUM(quantity)** per kombinasi pelanggan + produk:

| Net | Makna bisnis |
|---|---|
| **> 0** | Pelanggan masih memegang kontainer milik kita yang belum dikembalikan |
| **= 0** | Seimbang — tidak ada yang berhutang kontainer |
| **< 0** | Kita memegang kontainer kosong milik pelanggan — wajib dikirim kembali sebagai galon terisi pada pengiriman berikutnya |

**Contoh Kasus 1 — Bu Ani (net negatif — kita berutang ke pelanggan)**
Kurir antar 10 galon Aqua terisi, Bu Ani kembalikan 20 galon kosong.
- Loan +10 (10 terisi diantar)
- Return −20 (20 kosong diterima)
- **Net = −10** → Kita memegang 10 kontainer Bu Ani; wajib isi dan antar 10 galon ekstra di kunjungan berikutnya.

**Contoh Kasus 2 — Pak Joko (net positif — pelanggan berutang ke kita)**
Kurir antar 20 galon Aqua terisi, Pak Joko kembalikan 10 galon kosong.
- Loan +20 (20 terisi diantar)
- Return −10 (10 kosong diterima)
- **Net = +10** → Pak Joko masih memegang 10 kontainer milik kita yang belum dikembalikan.

### 12.1 User Stories

- As an Owner, I want to see which customers hold our unreturned containers (net > 0) so I can follow up.
- As an Owner, I want to see which customers have returned more empties than filled deliveries (net < 0) so I know how many filled containers I owe them on the next visit.
- As an Owner, I want to manually record a standalone container return when a customer brings empties back outside of a delivery transaction.

### 12.2 Functional Requirements

**FR-CON-001 — Container loan at transaction creation**
Handled by FR-TXN-001: for each `refillable` product sold in a transaction that has a named customer, a `ContainerLoans` record with **positive** `quantity` (= qty sold) is created automatically, and a `dispatch` `StockMovements` record is created to track the filled stock leaving the source location. This establishes the baseline of how many filled containers left our possession; subsequent return records (negative quantities) reduce this baseline to arrive at the net outstanding balance.

**FR-CON-002 — Container return during transaction**
On Langkah 3 of the transaction form, when a customer is selected and refillable products are in the cart, staff can optionally record empty containers being returned in the same visit. The input has **no maximum constraint** — customers may return more empties than the current delivery (e.g. returning containers from a previous visit). For each return entry a `ContainerLoans` record with **negative** `quantity` (= qty returned) is created, and a `receive` `StockMovements` record (`container_status = 'empty'`) at the source location is created, both linked to the transaction.

**FR-CON-003 — Standalone container return**
Owner can manually record a container return outside any transaction via the Kontainer tab in StockPage (calls `POST /api/container-loans` with negative quantity). Used when a customer drops off empties without making a purchase.

**FR-CON-004 — Outstanding net per customer**
Computed: `SUM(ContainerLoans.quantity WHERE customer AND product)`. The result can be positive, zero, or negative (see §12.0). Shown on the Kontainer tab and on the customer detail page.

**FR-CON-005 — Container loans tab in StockPage**
Owner can view **all** customers with a non-zero net container balance via the **"Kontainer"** tab in the Stock page. The tab is split into two visual sections:
- **Net > 0 (orange/amber):** Customer holds our unreturned containers — show "[CustomerName] masih memegang [N] kontainer kami" with a "Catat Pengembalian" inline form.
- **Net < 0 (blue/info):** We hold customer's excess empties — show "[N] kontainer milik [CustomerName] ada di truk — kembalikan saat pengiriman berikutnya." No standalone action button; resolved via the next delivery transaction.

### 12.3 Validation Rules

| ID | Field | Rule | Error Message |
|---|---|---|---|
| VAL-CON-001 | `product_id` | Required, `refillable` products only | `"Hanya produk refillable yang bisa dipinjamkan."` |
| VAL-CON-002 | `customer_id` | Required, active customer | `"Pelanggan wajib dipilih."` |
| VAL-CON-003 | `quantity` | Required, non-zero integer | `"Jumlah wajib diisi dan tidak boleh nol."` |

### 12.4 UI Behavior

- **Transaction form Langkah 3 — "Kontainer Kosong Diterima dari Pelanggan (opsional)":** One input per refillable product in the cart. Label: "[ProductName]". Hint: "Bisa lebih dari jumlah yang dijual jika pelanggan memberikan kontainer ekstra". No maximum constraint.
- **StockPage Kontainer tab (Owner only):**
  - **Positive-net rows (orange):** "[CustomerName] memegang [N] kontainer kami" — inline "Catat Pengembalian" form with qty input and confirm button.
  - **Negative-net rows (blue):** "[abs(N)] kontainer milik [CustomerName] ada di truk kami — kembalikan saat pengiriman berikutnya." No action button.
  - Rows are grouped by customer; each product is a separate row.
- **Customer detail — loans section:** Outstanding net per product; "Catat Pengembalian Kontainer" button.
- **Empty state (Kontainer tab):** `"Tidak ada transaksi kontainer aktif."`

---

## 13. Module: Dashboard (FR-DSH)

**Accessible by:** All authenticated roles (owner, kasir, kurir). Kasir and kurir see a user-scoped view; certain owner-only sections are hidden.

### 13.1 User Stories

- As an Owner, I want to see today's full business summary at a glance.
- As an Owner, I want to see a weekly bar chart of revenue so I can track performance over the week.
- As an Owner, I want to filter the dashboard by a selected date so I can review historical summaries.
- As an Owner, I want the dashboard to refresh automatically so I see live activity.
- As a Kasir or Kurir, I want to see my own today's performance (revenue and transaction count) so I can track my activity.
- As a Kasir or Kurir, I want to see the store's low-stock count and warehouse inventory so I can act on stock issues.
- As a Kasir or Kurir, I want to see the store's customer debt summary so I know which customers owe money.

### 13.2 Functional Requirements

**FR-DSH-001 — Polling**
Polls `GET /api/dashboard` every **5 seconds** when active. Pauses on hidden tab.

**FR-DSH-002 — Summary statistics**
- Pendapatan hari ini (sum of `paid_amount` from transactions today)
- Total pembelian dari vendor hari ini (sum of `purchase_cost` from `receive` movements today)
- Pembayaran hutang diterima hari ini (sum of `DebtPayments.amount` recorded today)
- Produk stok rendah (warehouse stock ≤ 5 units)
- Pendapatan hari sebelumnya (`previous_day_revenue`) — included in API response for revenue % delta computation on the frontend

**FR-DSH-003 — Recent transactions**
10 most recent transactions: waktu, pelanggan, staff, tipe, total, status badge, lunas/utang indicator.

**FR-DSH-004 — Warehouse stock summary**
Current warehouse stock for all active products. Shown store-wide to all roles.

**FR-DSH-005 — Role-scoped access** *(updated by FR-DSH-011)*
All authenticated roles may access `/dashboard`. Kasir and kurir see a user-scoped view (see FR-DSH-011). The previous redirect behaviour (kurir and kasir sent to `/transactions` on navigation to `/dashboard`) is removed; the Bottom Navigation bar now includes a Dashboard item as the first entry for kasir and kurir.

**FR-DSH-006 — Date filter**
The dashboard shall include a date picker that controls which date's data is displayed across all summary statistics and the weekly bar chart. Default value is today's date. When the Owner changes the selected date:
- Summary statistics (FR-DSH-002) recalculate for the selected date
- The weekly bar chart (FR-DSH-007) shifts its 7-day window to end on the selected date
- Recent transactions (FR-DSH-003) show the 10 most recent transactions on or before the selected date
- The API accepts an optional `date` query parameter (ISO 8601 date string, e.g. `2026-05-07`); when omitted, defaults to the server's current date in WIB (UTC+7)

**FR-DSH-007 — Weekly bar chart**
The dashboard shall display an interactive bar chart (Chart.js via `react-chartjs-2`) showing daily revenue (sum of `paid_amount`) for the 7-day window ending on the selected date (inclusive). Each bar represents one day. The chart data is included in the `GET /api/dashboard?date=YYYY-MM-DD` response as a `weekly_chart` array of 7 objects: `{ date, revenue, transaction_count, purchase_cost }`, ordered oldest to newest.

Each bar is **clickable**: clicking a bar opens an inline detail panel below the chart showing that day's revenue, transaction count, purchase cost, and average revenue per transaction. Clicking the same bar again or the ✕ button closes the panel. Changing the date filter closes any open detail panel.

**FR-DSH-008 — Clickable recent transaction rows**
Each row in the Transaksi Terkini section shall be tappable/clickable. Tapping a row opens a **Detail Transaksi** modal showing: transaction type, status badge, date/time, customer name (if any), stock location, payment method, dibuat oleh, optional notes, item list (product, qty × unit price, subtotal), total, paid amount, and outstanding debt (if any). The modal is identical in content to the transaction detail view in the Transactions page. The full `Transaction` object is fetched via `GET /api/transactions/{id}` on row click (lazy load).

**FR-DSH-009 — Hutang Pelanggan section**
The dashboard shall include a **"Hutang Pelanggan"** section positioned below Transaksi Terkini and above Stok Gudang. This section lists all active customers with `outstanding_debt > 0`, sorted by debt amount descending. Each row shows: customer name (left) and outstanding debt in danger red (right). This section is **not affected by the date filter** — it always shows the current live state of customer debts (same behavior as the Stok Gudang section). The data is served in the `GET /api/dashboard` response as a `customer_debts` array: `[{ customer_id, customer_name, outstanding_debt }]`. Empty state: `"Tidak ada hutang pelanggan aktif."`

**FR-DSH-010 — Pendapatan per Staf pie chart** *(owner only)*
The dashboard shall include a **"Pendapatan per Staf"** section positioned immediately below "Pendapatan 7 Hari" and above "Transaksi Terkini". This section shows a pie chart breaking down revenue (`paid_amount`) by the staff member who created each completed transaction for the selected date. Only transactions with `status = 'completed'` are counted. Each slice corresponds to one staff member; the legend displays each staff name alongside their percentage share of total revenue. The tooltip on hover shows the formatted Rupiah amount and percentage. Staff members with zero revenue for the selected date are excluded from the chart. Empty state (no completed transactions at all): `"Tidak ada data pendapatan untuk hari ini."` The data is served in the `GET /api/dashboard` response as a `staff_revenue` array: `[{ staff_id, staff_name, revenue, transaction_count }]`, sorted by revenue descending. **This section is hidden for kasir and kurir; `staff_revenue` is always returned as an empty array for non-owner callers.**

**FR-DSH-011 — Role-based dashboard access and user-scoped stats**
The dashboard is accessible to all authenticated roles (owner, kasir, kurir). For **kasir and kurir**, the following rules apply:

*Server-side scoping (backend):*
- `today_revenue` — sum of `paid_amount` from the caller's own completed transactions on `date` (`Transaction.StaffId = caller.userId`).
- `today_transactions` — count of the caller's own completed transactions on `date`.
- `today_purchase_cost` — sum of `purchase_cost` from stock movements created by the caller on `date` (`StockMovement.CreatedBy = caller.userId`).
- `today_debt_collected` — sum of debt payments recorded by the caller on `date` (`DebtPayment.CreatedBy = caller.userId`).
- `previous_day_revenue` — same scoping as `today_revenue` but for the previous day.
- `weekly_chart` — each entry's `revenue`, `transaction_count`, and `purchase_cost` are scoped to the caller's own records for that day.
- `recent_transactions` — filtered to the caller's own transactions on `date`.
- `staff_revenue` — always an empty array for kasir/kurir.
- `low_stock_count`, `total_outstanding_debt`, `warehouse_stock`, `customer_debts` — always store-wide (same for all roles).

*Frontend-side hiding:*
- **Biaya Pembelian stat card** — hidden for kasir and kurir.
- **Biaya Pembelian row in the bar chart detail panel** — hidden for kasir and kurir.
- **Pendapatan per Staf section** — hidden for kasir and kurir (FR-DSH-010).

*Navigation:*
- `/dashboard` route is accessible to `['owner', 'kasir', 'kurir']`.
- Bottom Navigation for kasir and kurir gains a **Dashboard** item as the first entry (was 4 items; now 5).

### 13.3 UI Behavior

- **Layout:** 2×3 stat card grid on mobile (2 columns, 3 rows), wider grid on tablet+. For kasir/kurir the Biaya Pembelian card is hidden, resulting in a 2×2 (or adjusted) grid.
- **Date filter:** Date picker rendered at the top of the dashboard page; defaults to today. Changing the date immediately re-fetches `GET /api/dashboard?date=YYYY-MM-DD` and updates all sections. A `"Hari Ini"` shortcut button resets the filter to today.
- **Revenue % delta:** The Pendapatan card displays a secondary line showing `↑ +X%` (green) or `↓ -X%` (red) comparing today's revenue to the previous day (`previous_day_revenue`). Hidden when previous day revenue = 0.
- **Weekly bar chart:** Interactive Chart.js vertical bar chart — 7 bars, one per day; x-axis short day labels (`Sen`, `Sel`, …); y-axis abbreviated IDR; selected-date bar is highlighted with a darker shade; each bar is clickable.
- **Chart detail panel:** Appears inline below the chart when a bar is clicked — shows date, revenue, transaction count, and average per transaction for that day. The **Biaya Pembelian row is hidden for kasir and kurir**. Dismissed by clicking the same bar again or ✕. Resets on date filter change.
- **Stat cards (owner):** Pendapatan, Biaya Pembelian, Pembayaran Hutang Diterima, Total Hutang Pelanggan, Transaksi, Stok Rendah.
- **Stat cards (kasir/kurir):** Pendapatan, Pembayaran Hutang Diterima, Total Hutang Pelanggan, Transaksi, Stok Rendah (Biaya Pembelian hidden).
- **Last updated:** `"Terakhir diperbarui: Xm Xs yang lalu"`
- **Low stock:** Warning badge on products ≤ 5
- **Recent transactions:** 10 rows inline, not paginated; each row is **clickable** to open a Detail Transaksi modal (FR-DSH-008). For kasir/kurir only their own transactions are shown.
- **Customer debt section (FR-DSH-009):** Below Transaksi Terkini; always shows current store-wide state; not date-filtered; sorted by debt descending. Shown to all roles.
- **Staff revenue pie chart (FR-DSH-010):** Positioned below "Pendapatan 7 Hari"; filtered by selected date; only completed transactions; staff with revenue = 0 excluded; legend shows name + %; empty state: `"Tidak ada data pendapatan untuk hari ini."` **Hidden entirely for kasir and kurir.**
- **Empty state (chart):** `"Tidak ada data pendapatan untuk minggu ini."`
- **Empty state (transactions):** `"Belum ada transaksi tercatat pada tanggal ini."`

---

## 14. Permission Matrix

| Feature | Owner | Kurir | Kasir |
|---|---|---|---|
| Login / Logout | ✅ | ✅ | ✅ |
| **Users** — Manage | ✅ | ❌ | ❌ |
| **Locations** — Manage | ✅ | Lihat kendaraan sendiri | ❌ |
| **Products** — Manage | ✅ | ❌ | ❌ |
| **Products** — View (forms) | ✅ | ✅ | ✅ |
| **Customers** — Manage | ✅ | ✅ | ✅ |
| **Customers** — Pricing | ✅ | ❌ | ❌ |
| **Customers** — View (forms) | ✅ | ✅ | ✅ |
| **Stock** — View warehouse | ✅ | ✅ | ✅ |
| **Stock** — View own truck | ✅ | ✅ | ❌ |
| **Stock** — View other trucks | ✅ | ❌ | ❌ |
| **Stock** — Receive / defect | ✅ | ❌ | ❌ |
| **Stock** — Truck load / return | ✅ | ✅ | ❌ |
| **Stock** — Vendor Exchange | ✅ | ✅ | ❌ |
| **Stock** — Movement history | ✅ | ❌ | ❌ |
| **Transactions** — View all | ✅ | ❌ | ❌ |
| **Transactions** — View own | ✅ | ✅ | ✅ |
| **Transactions** — Create delivery | ✅ | ✅ | ❌ |
| **Transactions** — Create counter | ✅ | ❌ | ✅ |
| **Transactions** — Create vendor-direct | ✅ | ❌ | ✅ |
| **Transactions** — Cancel own pending | ✅ | ✅ | ✅ |
| **Transactions** — Cancel any / update status | ✅ | ❌ | ❌ |
| **Payments** — View own txn status | ✅ | ✅ | ✅ |
| **Payments** — Add payment on any txn | ✅ | ❌ | ❌ |
| **Payments** — Standalone debt payment | ✅ | ❌ | ❌ |
| **Container Loans** — View / standalone return | ✅ | ❌ | ❌ |
| **Container Loans** — Return during transaction | ✅ | ✅ | ✅ |
| **Dashboard** | ✅ (full view) | ✅ (user-scoped, owner-only sections hidden) | ✅ (user-scoped, owner-only sections hidden) |

---

## 15. Data Validation Reference

| Entity | Field | Type | Constraints | Required |
|---|---|---|---|---|
| User | `name` | VARCHAR(100) | max 100 | Yes |
| User | `username` | VARCHAR(50) | unique, alphanumeric + underscore | Yes |
| User | `password` | — | min 8 chars | Yes on create |
| User | `role` | ENUM | `owner`, `kurir`, `kasir` | Yes |
| Location | `name` | VARCHAR(100) | max 100 | Yes |
| Location | `type` | ENUM | `warehouse`, `vehicle` | Yes |
| Location | `assigned_to` | UUID | active user (owner or kurir); required if vehicle | Conditional |
| Product | `name` | VARCHAR(100) | max 100 | Yes |
| Product | `category` | ENUM | `simple`, `refillable` | Yes |
| Product | `production_type` | ENUM | `purchased`, `selfproduced`; required if refillable | Conditional |
| Product | `type` | ENUM | `air`, `gas` | Yes |
| Product | `unit` | VARCHAR(20) | max 20 | Yes |
| Product | `base_price` | DECIMAL(15,2) | positive | Yes |
| Customer | `name` | VARCHAR(100) | max 100 | Yes |
| Customer | `phone` | VARCHAR(20) | max 20, digits/spaces/+/-; optional | No |
| Customer | `address` | TEXT | max 500 | No |
| CustomerPricing | `custom_price` | DECIMAL(15,2) | positive | No |
| StockMovement | `product_id` | UUID | active product | Yes |
| StockMovement | `movement_type` | ENUM | `receive`, `transfer`, `dispatch`, `defect` | Yes |
| StockMovement | `container_status` | ENUM | `filled`/`empty` (refillable) or `na` (simple) | Yes |
| StockMovement | `quantity` | INT | positive, min 1 | Yes |
| StockMovement | `purchase_cost` | DECIMAL(15,2) | positive; required for vendor exchange | Conditional |
| StockMovement | `note` | TEXT | max 255; required for defect | Conditional |
| Transaction | `transaction_type` | ENUM | `delivery`, `counter`, `vendor_direct` | Yes |
| Transaction | `customer_id` | UUID | active customer | Yes |
| Transaction | `items` | Array | min 1 | Yes |
| Transaction | `payment_method` | ENUM | `cash`, `transfer`, `qris` | Yes |
| Transaction | `paid_amount` | DECIMAL(15,2) | 0 ≤ paid_amount ≤ total_amount | Yes |
| Transaction | `notes` | TEXT | max 500 | No |
| TransactionItem | `quantity` | INT | positive, min 1 | Yes |
| Payment | `amount` | DECIMAL(15,2) | positive, ≤ remaining unpaid | Yes |
| Payment | `reference_no` | VARCHAR(100) | max 100 | No |
| DebtPayment | `customer_id` | UUID | active customer | Yes |
| DebtPayment | `amount` | DECIMAL(15,2) | positive | Yes |
| DebtPayment | `reference_no` | VARCHAR(100) | max 100 | No |
| ContainerLoan | `product_id` | UUID | refillable only | Yes |
| ContainerLoan | `customer_id` | UUID | active customer | Yes |
| ContainerLoan | `quantity` | INT | non-zero integer | Yes |

---

## 16. UI Behavioral Requirements

### 16.1 Global — All Screens Are Mobile-First
All roles (Owner, Kurir, Kasir) use mobile phones as their primary device. No role has a desktop-primary screen. Rules:
- Bottom navigation bar for all roles (max 5 items per role)
- Minimum tap target: **44×44px** for all interactive elements
- Action buttons (Kirim, Simpan, Lanjut) anchored to the bottom of the screen
- All pickers (customer, product, location) use full-screen bottom-sheet modals with search input
- Minimum font size: **16px** for input fields (prevents iOS auto-zoom)
- No horizontal scrolling on any page
- **Logout:** accessible via the `/lainnya` hub page (owner) or the `/profile` page (kurir, kasir); not present in the top navigation bar

### 16.2 Global Loading States
- In-flight requests: spinner/skeleton within the affected component
- First page load: 5 skeleton rows while fetching
- Polling refresh: silent data update, no spinner

### 16.3 Global Error States

| HTTP Status | Behavior |
|---|---|
| 401 | Redirect to login — `"Sesi Anda telah berakhir. Silakan masuk kembali."` |
| 403 | Inline: `"Anda tidak memiliki izin untuk melakukan tindakan ini."` |
| 422 / 400 | Field-level errors inline under each field |
| 500 | Toast: `"Terjadi kesalahan. Silakan coba lagi."` |
| Network error | Toast: `"Tidak dapat terhubung. Periksa koneksi internet Anda."` |

### 16.4 Currency Formatting
All monetary values in IDR via `formatCurrency.js`:
- `1500000` → `Rp1.500.000`
- `1500000.50` → `Rp1.500.000,50`

### 16.5 Date / Time Formatting
- All timestamps in **WIB (UTC+7)**
- List format: `6 Mei 2026, 14:30`
- Relative (dashboard): `"2 menit yang lalu"`, `"1 jam yang lalu"`

### 16.6 PWA — Home Screen Install
- `manifest.json` + service worker via `vite-plugin-pwa`
- App name: `"POS Water & Gas"`
- One-time install prompt banner on first mobile visit: `"Pasang aplikasi ini untuk akses cepat di layar utama Anda."`
- No offline functionality in Phase 1 (deferred to Phase 3)

---

## 17. Non-Functional Requirements

| # | Requirement | Target |
|---|---|---|
| NFR-001 | API response time (list endpoints) | < 500ms under normal load |
| NFR-002 | Stock / dashboard polling interval | 5 seconds active, paused on hidden tab |
| NFR-003 | Concurrent users | 1 Owner + up to 5 Kurir/Kasir |
| NFR-004 | Security | Per ARCHITECTURE.md §7 (OWASP Top 10, JWT, BCrypt, CORS, HTTPS, rate limiting) |
| NFR-005 | Free-tier storage | Within Neon.tech 0.5 GB |
| NFR-006 | Browser support | Chrome for Android (latest) primary; Chrome/Edge desktop secondary |
| NFR-007 | Deployment | Zero-cost: Vercel + MonsterASP.NET + Neon.tech |
| NFR-008 | Mobile UI | All screens: 44px tap target, no horizontal scroll, no iOS auto-zoom |

---

## 18. Settings / Lainnya Navigation (FR-SET)

**FR-SET-001 — Bottom nav label rename**
- The bottom navigation item previously labelled "Pengaturan" (gear icon) is renamed to **"Lainnya"** for all roles that show it (owner).
- The Settings hub page H1 heading changes from "Pengaturan" to "Lainnya".
- Frontend folder renamed: `src/pages/Settings/` → `src/pages/Lainnya/`; files renamed: `SettingsPage.tsx` → `LainnyaPage.tsx`, `SettingsPage.module.scss` → `LainnyaPage.module.scss`.
- The route has been renamed from `/settings` to `/lainnya`; all sub-path active-state logic applies to `/lainnya`.
- The Lainnya hub card list now includes a new **Arus Kas** card (`/cash-flow`) — see FR-CSH.

---

## 19. Debt Payments Enhancements (FR-DBT)

**FR-DBT-001 — Two-tab layout**
- The Debt Payments page (`/debt-payments`) is restructured into two tabs: **"Hutang Aktif"** and **"Riwayat"**.
- The Riwayat tab is lazy-loaded on first open (mirrors FR-TXN-015 / FR-STK-012 pattern).

**FR-DBT-002 — Hutang Aktif tab**
- Lists all active customers (`is_active = true`) whose `outstanding_debt > 0`, sorted by debt descending.
- Each row is a tappable button displaying customer name, phone (if available), and outstanding balance in danger color.
- Tapping a row navigates to the per-customer detail sub-page: `/debt-payments/:customerId`.
- The "+ Catat Pembayaran" button is scoped to this tab (not in the page header).

**FR-DBT-003 — Riwayat tab**
- Displays standalone debt payment records (`DebtPayment` entries) filtered by a date picker.
- Default date = today WIB; date input + conditional "Hari Ini" shortcut button (same pattern as FR-TXN-015).
- Selecting a new date immediately re-fetches the list.

**FR-DBT-004 — Per-customer debt detail sub-page**
- Route: `/debt-payments/:customerId` (owner and kasir roles).
- Page title: customer name; subtitle: "Riwayat Hutang Pelanggan".
- **Outstanding debt banner**: red if `outstanding_debt > 0`; green ("Hutang Lunas") if cleared.
- **Transaksi Pembuat Hutang section**: lists transactions where `total_amount > paid_amount` and status ≠ cancelled for this customer, sorted newest first. Each row shows transaction type badge, date, total / paid / remaining debt amounts.
- **Pembayaran Hutang section**: lists standalone `DebtPayment` records for this customer, sorted newest first. Each row shows amount (green), date, staff name, and optional note.
- "+ Catat Pembayaran" button in header; opens the same create-payment modal as the list page.
- Back button returns to `/debt-payments`.
- New API endpoint: `GET /api/customers/{id}/debt-history` — response: `CustomerDebtHistory` (see §20 types).

**FR-DBT-005 — Dashboard debt rows clickable**
- Each customer row in the Dashboard "Hutang Pelanggan" section is now tappable (role="button", keyboard-accessible via onKeyDown Enter/Space).
- Tapping navigates to `/debt-payments/:customer_id`.
- A right-pointing chevron icon is displayed at the row's trailing edge.

**FR-DBT-007 — Initial customer outstanding debt**
- A customer record may carry an `initial_debt` value (≥ 0, in Rupiah) representing a pre-existing balance brought forward from paper records (one-time migration use).
- The **Owner** can set `initial_debt` when creating a new customer or editing an existing one via the "Saldo Hutang Awal (Rp, opsional)" numeric input in the customer form. The field is hidden from Kasir and Kurir roles.
- `outstanding_debt` is now computed as: `initial_debt + SUM(transactions.debt_amount WHERE status ≠ cancelled) - SUM(debt_payments.amount)`.
- `GET /api/customers/{id}/debt-history` response now includes an `initial_debt` field. The per-customer debt detail page (`CustomerDebtDetailPage`) displays an amber info row "Saldo Awal Hutang" when `initial_debt > 0`.
- Validation: `initial_debt` must be ≥ 0; negative values are rejected with HTTP 400.
- Existing customers without a set value default to `initial_debt = 0` (no behaviour change).

**FR-DBT-006 — Catat Pembayaran modal — payment method field**
- The "Catat Pembayaran" create-payment modal (used on both `/debt-payments` and `/debt-payments/:customerId`) must include a **Metode Pembayaran** selector with three options: **Tunai** (`cash`), **Transfer** (`transfer`), **QRIS** (`qris`). Default: Tunai.
- The selected method is sent to `POST /api/debt-payments` as the required `method` field.
- The Catatan field remains optional.

---

## 20. Cash Flow Page (FR-CSH)

**FR-CSH-001 — Page access and navigation**
- New page at route `/cash-flow`, accessible to **owner** role only.
- Accessible from the Lainnya hub ("Arus Kas" card with cash-flow SVG icon).
- Back button in page header navigates to `/lainnya`.
- Bottom nav active state: the "Lainnya" nav item remains active for `/cash-flow` (included in `SETTINGS_SUB_PATHS`).

**FR-CSH-002 — Date filter**
- Date input (`<input type="date">`) + conditional "Hari Ini" shortcut button.
- Default date = today WIB (`Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Jakarta' })`).
- Selecting a new date immediately re-fetches all data for that date.

**FR-CSH-003 — Summary cards**
Four summary cards displayed in a 2×2 grid:
| Card | Color | Value |
|---|---|---|
| Kas Masuk | Green | Sum of all `cash_in` entries |
| Kas Keluar | Red | Sum of all `cash_out` entries |
| Net Kas | Blue (positive) / Red (negative) | Kas Masuk − Kas Keluar |
| Piutang Baru | Amber | Sum of all `new_debt` entries |

**FR-CSH-004 — Entry list with three flow types**
Each entry in the list has a left-colored border indicating its flow type:

| Flow Type | Color | Category | Source |
|---|---|---|---|
| `cash_in` | Green | `sale_payment` | `paid_amount` portion of a completed transaction |
| `cash_in` | Green | `debt_payment` | Standalone `DebtPayment` record |
| `cash_out` | Red | `stock_purchase` | `StockMovement` with `purchase_cost > 0` (Receive / Vendor Exchange) |
| `new_debt` | Amber | `debt_created` | `debt_amount` portion of a transaction (`total_amount − paid_amount`) |

Each row shows: flow-type badge, description (e.g. "Penjualan – Toko Sedap"), staff name · time, and amount with +/− prefix and matching color.
Entries are sorted newest first within the selected date.

New API endpoint: `GET /api/cash-flow?date=YYYY-MM-DD` — defaults to today WIB; response: `CashFlowSummary`.

**FR-CSH-005 — Monthly export to XLSX**
- A "Unduh Laporan Bulanan" section is displayed below the date filter on the Arus Kas page (owner only).
- Contains a month/year picker (`<input type="month">`) defaulting to the current month (max = current month) and an "Unduh .xlsx" button.
- On click: fetches all entries for the selected calendar month via `cashFlowService.getRange(firstDay, lastDay)`, then generates and downloads a `.xlsx` file client-side using SheetJS (`xlsx` v0.18+, Apache-2.0).
- File name format: `LaporanArusKas_YYYY-MM_diekspor_YYYYMMDD.xlsx`.
- The downloaded file contains two sheets:
  1. **Ringkasan Bulanan** — report title, export timestamp, aggregate summary block (4 rows), then a daily breakdown table (one row per calendar day, plus a Total row). Columns: Tanggal | Kas Masuk (Rp) | Kas Keluar (Rp) | Net Kas (Rp) | Piutang Baru (Rp). Days with no activity show zeros.
  2. **Detail Transaksi** — all individual entries sorted oldest first. Columns: No | Tanggal | Waktu | Jenis Arus | Kategori | Keterangan | Dicatat Oleh | Jumlah (Rp). Amounts stored as plain numbers for native Excel summation.
- While the export is in progress, the button shows a spinner and "Memproses…" text and is disabled.
- On error, a toast notification is shown.
- The month picker is independent of the day-level date filter — they do not affect each other.
