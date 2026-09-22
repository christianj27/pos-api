# API Contract — POS App
> MSMe Water & Gas | Version 1.2 | Last updated: September 21, 2026

---

## Common Conventions

### Base URL
| Environment | URL |
|---|---|
| Development | `http://localhost:5000` |
| Production | Value of `VITE_API_BASE_URL` environment variable |

### Authentication
All protected endpoints require the `Authorization` header:
```
Authorization: Bearer <access_token>
```
The refresh token is stored in an `HttpOnly; Secure; SameSite=Strict` cookie and is never accessible from JavaScript.

### Request Format
- `Content-Type: application/json` for all POST/PUT requests
- Dates in query params: `YYYY-MM-DD` (interpreted as WIB / Asia/Jakarta, UTC+7)
- All IDs: UUID v4 strings

### Response Format
- All timestamps: ISO 8601 UTC string (e.g. `2026-05-14T03:00:00Z`)
- All monetary amounts: `number` (decimal, 2 decimal places, Rupiah)
- All JSON property names: **snake_case**

### Standard Error Response
```json
{
  "message": "Human-readable error description",
  "errors": {
    "fieldName": ["Validation error detail"]
  }
}
```
The `errors` object is only present on validation failures (`400`).

### HTTP Status Codes
| Code | Meaning |
|---|---|
| `200` | Success with response body |
| `201` | Resource created; response body contains new resource |
| `204` | Success; no response body |
| `400` | Validation error; see `errors` in response body |
| `401` | Missing, expired, or invalid access token |
| `403` | Authenticated but insufficient role |
| `404` | Resource not found |
| `409` | Conflict (e.g. duplicate username) |
| `429` | Rate limit exceeded |
| `500` | Internal server error |

### Authorization Roles
| Label used in this document | Who can access |
|---|---|
| **All roles** | `owner`, `kurir`, `kasir` (any authenticated user) |
| **Owner/Kasir** | `owner` or `kasir` |
| **Owner/Kurir** | `owner` or `kurir` |
| **Owner only** | `owner` only |
| **Anonymous** | No authentication required |

---

## 1. Auth

### POST /api/auth/login
**Auth**: Anonymous  
**Rate limit**: max 10 attempts per IP per 15 minutes

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `username` | string | ✅ | max 50 chars |
| `password` | string | ✅ | — |

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `access_token` | string | JWT; valid for 60 minutes |
| `role` | string | `owner` \| `kurir` \| `kasir` |
| `user_id` | string (UUID) | — |
| `name` | string | Display name |
| `username` | string | — |

> Sets `HttpOnly; Secure; SameSite=Strict` cookie named `refresh_token` (valid 7 days).

---

### POST /api/auth/refresh
**Auth**: Anonymous (reads `refresh_token` HttpOnly cookie)

No request body.

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `access_token` | string | New JWT; valid for 60 minutes |

> Rotates the refresh token: old cookie is revoked server-side, a new cookie is issued.

---

### POST /api/auth/logout
**Auth**: All roles

No request body.

**Response `204`** — No content.

> Revokes the refresh token in the database and clears the cookie.

---

## 2. Profile

### GET /api/profile
**Auth**: All roles

Returns the authenticated user's own record only.

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `name` | string | — |
| `username` | string | — |
| `role` | string | `owner` \| `kurir` \| `kasir` |

---

### PUT /api/profile
**Auth**: All roles

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ❌ | max 100 chars |
| `current_password` | string | Conditional | Required when `new_password` is provided |
| `new_password` | string | ❌ | — |

> `role` and `username` are immutable via this endpoint.

**Response `200`** — same shape as `GET /api/profile`.

---

## 3. Users

### GET /api/users
**Auth**: Owner only

**Response `200`** — array of User objects.

**User object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `name` | string | — |
| `username` | string | — |
| `role` | string | `owner` \| `kurir` \| `kasir` |
| `is_active` | boolean | — |
| `created_at` | string (ISO 8601) | — |

---

### POST /api/users
**Auth**: Owner only

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ✅ | max 100 chars |
| `username` | string | ✅ | max 50 chars; must be unique |
| `password` | string | ✅ | — |
| `role` | string | ✅ | `owner` \| `kurir` \| `kasir` |

**Response `201`** — User object.

---

### PUT /api/users/{id}
**Auth**: Owner only

**Path Params**: `id` — UUID of the user.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ❌ | — |
| `username` | string | ❌ | must be unique |
| `password` | string | ❌ | Supply to change the password |
| `role` | string | ❌ | `owner` \| `kurir` \| `kasir` |
| `is_active` | boolean | ❌ | Set `false` to deactivate |

**Response `200`** — updated User object.

---

### DELETE /api/users/{id}
**Auth**: Owner only

**Path Params**: `id` — UUID of the user.

**Response `204`** — Soft deactivates the user (`is_active = false`). No content.

---

## 4. Locations

### GET /api/locations
**Auth**: All roles

**Response `200`** — array of Location objects.

**Location object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `name` | string | — |
| `type` | string | `warehouse` \| `vehicle` |
| `assigned_to` | string (UUID) \| null | User assigned to this vehicle; null for warehouses |
| `assigned_user_name` | string \| null | Display name of the assigned user |
| `is_active` | boolean | — |
| `created_at` | string (ISO 8601) | — |

---

### POST /api/locations
**Auth**: Owner only

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ✅ | max 100 chars |
| `type` | string | ✅ | `warehouse` \| `vehicle` |
| `assigned_to` | string (UUID) | Conditional | Required when `type = vehicle` |

**Response `201`** — Location object.

---

### PUT /api/locations/{id}
**Auth**: Owner only

**Path Params**: `id` — UUID of the location.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ❌ | — |
| `assigned_to` | string (UUID) \| null | ❌ | Pass `null` to unassign |
| `is_active` | boolean | ❌ | Set `false` to deactivate |

**Response `200`** — updated Location object.

---

## 5. Products

### GET /api/products
**Auth**: All roles

**Response `200`** — array of Product objects.

**Product object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `name` | string | — |
| `category` | string | `simple` \| `refillable` |
| `production_type` | string \| null | `purchased` \| `selfproduced`; only present for `refillable` products |
| `type` | string | `air` \| `gas` |
| `unit` | string | e.g. `galon`, `tabung`, `karton` |
| `base_price` | number | Rupiah |
| `is_active` | boolean | — |
| `created_at` | string (ISO 8601) | — |

---

### POST /api/products
**Auth**: Owner only

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ✅ | max 100 chars |
| `category` | string | ✅ | `simple` \| `refillable` |
| `production_type` | string | Conditional | Required when `category = refillable` |
| `type` | string | ✅ | `air` \| `gas` |
| `unit` | string | ✅ | max 20 chars |
| `base_price` | number | ✅ | ≥ 0 |

**Response `201`** — Product object.

---

### PUT /api/products/{id}
**Auth**: Owner only

**Path Params**: `id` — UUID of the product.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ❌ | — |
| `category` | string | ❌ | `simple` \| `refillable` |
| `production_type` | string \| null | ❌ | — |
| `type` | string | ❌ | `air` \| `gas` |
| `unit` | string | ❌ | — |
| `base_price` | number | ❌ | ≥ 0 |
| `is_active` | boolean | ❌ | — |

**Response `200`** — updated Product object.

---

### DELETE /api/products/{id}
**Auth**: Owner only

**Path Params**: `id` — UUID of the product.

**Response `204`** — Soft deactivates the product (`is_active = false`). No content.

---

## 6. Customers

### GET /api/customers
**Auth**: All roles

**Query Params**
| Param | Type | Default | Notes |
|---|---|---|---|
| `active_only` | boolean | `true` | When `true` (default) only customers with `is_active = true` are returned. Pass `active_only=false` to include soft-deleted customers. |

**Response `200`** — array of Customer objects.

**Customer object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `name` | string | — |
| `phone` | string \| null | — |
| `address` | string \| null | — |
| `is_active` | boolean | — |
| `is_confidential` | boolean | `true` = only visible to owners; always `false` in responses for non-owner callers (field omitted from their view via server-side filtering) |
| `created_at` | string (ISO 8601) | — |
| `outstanding_debt` | number | Net debt: `initial_debt + SUM(transaction debt_amounts) - SUM(debt_payments)` (Rupiah) |
| `initial_debt` | number | Opening balance carried forward from paper records; `0` by default |

> ✅ **Known gap #1 (resolved)**: `outstanding_debt` is now included in the Customer object. Backend `CustomerResponse` computes it as `initial_debt + SUM(transactions.debt_amount) - SUM(debt_payments.amount)` per customer.

> ℹ️ **Confidential filtering**: When the caller's role is `kasir` or `kurir`, customers with `is_confidential = true` are **excluded entirely** from the response array. Owners receive all customers.

> ℹ️ **Soft-delete filtering**: Soft-deleted customers (`is_active = false`) are **excluded entirely** by default for every role. Use `?active_only=false` (owner tooling / admin) to retrieve them — needed e.g. to inspect a deleted customer's history. Deleted customers remain resolvable by id through the detail endpoints below (`/{id}/debt-history`, `/{id}/pricing`, `/{id}/container-loans`).

---

### POST /api/customers
**Auth**: Owner only

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ✅ | max 100 chars |
| `phone` | string | ❌ | max 20 chars |
| `address` | string | ❌ | — |
| `initial_debt` | number | ❌ | Opening balance ≥ 0; defaults to `0` |
| `is_confidential` | boolean | ❌ | `true` hides the customer from kasir/kurir; defaults to `false` |

**Response `201`** — Customer object.

---

### PUT /api/customers/{id}
**Auth**: Owner only

**Path Params**: `id` — UUID of the customer.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | ❌ | — |
| `phone` | string \| null | ❌ | — |
| `address` | string \| null | ❌ | — |
| `is_active` | boolean | ❌ | `false` soft-deletes the customer; prefer `DELETE /api/customers/{id}` |
| `initial_debt` | number | ❌ | Opening balance ≥ 0; replaces previous value when provided |
| `is_confidential` | boolean | ❌ | Owner only — ignored when sent by kasir/kurir |

**Response `200`** — updated Customer object.

> ⚠️ **Known gap #2 (resolved)**: Frontend `customerService.deactivate()` originally called `DELETE /api/customers/{id}` (no such route) and was temporarily rerouted to `PUT /api/customers/{id}` with `{ is_active: false }`. The `DELETE /api/customers/{id}` route now exists and performs the soft delete; the frontend calls `customerService.remove()`.

---

### DELETE /api/customers/{id}
**Auth**: All roles (controller-level `AllStaff` policy — same as `POST`/`PUT`)

**Path Params**: `id` — UUID of the customer.

**Response `204`** — Soft deletes the customer (`is_active = false`). No content.

**Behavior**
- The record is **retained**: historical transactions, `/{id}/debt-history`, container-loan balances and customer pricing stay intact — deletion never cascades.
- The customer disappears from `GET /api/customers` (default) and from every customer picker in the app.
- Deleting a customer **does not** clear their outstanding debt; `/{id}/debt` continues to report the same balance.
- There is no API to reactivate a deleted customer; restoration is a database-level operation.

**Error `400`** — customer not found: `{ "message": "Customer not found." }`

---

### GET /api/customers/{id}/pricing
**Auth**: Owner only

**Path Params**: `id` — UUID of the customer.

**Response `200`** — array of CustomerPricing items.

**CustomerPricing item**
| Field | Type | Notes |
|---|---|---|
| `product_id` | string (UUID) | — |
| `product_name` | string | — |
| `base_price` | number | Default product price |
| `custom_price` | number \| null | Price override for this customer; `null` means use `base_price` |

---

### PUT /api/customers/{id}/pricing
**Auth**: Owner only

**Path Params**: `id` — UUID of the customer.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `items` | array | ✅ | List of pricing overrides |
| `items[].product_id` | string (UUID) | ✅ | — |
| `items[].custom_price` | number \| null | ❌ | `null` removes the override and reverts to base price |

**Response `204`** — No content.

---

### GET /api/customers/{id}/debt
**Auth**: Owner only

**Path Params**: `id` — UUID of the customer.

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `customer_id` | string (UUID) | — |
| `customer_name` | string | — |
| `outstanding_debt` | number | Computed: `SUM(transaction debt_amount) - SUM(debt payments)` |

---

### GET /api/customers/{id}/container-loans
**Auth**: Owner only

**Path Params**: `id` — UUID of the customer.

Returns aggregated (net) container balances per product, not a raw event log.

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `customer_id` | string (UUID) | — |
| `customer_name` | string | — |
| `items` | array | Net balance per product |
| `items[].product_id` | string (UUID) | — |
| `items[].product_name` | string | — |
| `items[].unit` | string | — |
| `items[].net_quantity` | number | Positive = customer holds our containers; negative = we hold theirs; 0 = balanced |

---

### GET /api/customers/{id}/debt-history
**Auth**: Owner or Kasir

**Path Params**: `id` — UUID of the customer.

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `customer_id` | string (UUID) | — |
| `customer_name` | string | — |
| `initial_debt` | number | Opening balance carried forward; `0` if not set |
| `outstanding_debt` | number | Current computed balance (`initial_debt + tx_debts - payments`) |
| `debt_transactions` | array | Transactions that created debt; sorted newest first |
| `debt_transactions[].id` | string (UUID) | — |
| `debt_transactions[].created_at` | string (ISO 8601) | — |
| `debt_transactions[].type` | string | `delivery` \| `counter` |
| `debt_transactions[].total_amount` | number | — |
| `debt_transactions[].paid_amount` | number | — |
| `debt_transactions[].debt_amount` | number | `total_amount - paid_amount` |
| `debt_transactions[].created_by_name` | string | — |
| `payments` | array | Standalone debt payments; sorted newest first |
| `payments[].id` | string (UUID) | — |
| `payments[].amount` | number | — |
| `payments[].note` | string \| null | — |
| `payments[].created_by_name` | string | — |
| `payments[].created_at` | string (ISO 8601) | — |

---

## 7. Stock

### GET /api/stock/levels
**Auth**: All roles

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `location_id` | string (UUID) | ❌ | Filter by location; omit for all locations |

**Response `200`** — array of StockLevel objects.

**StockLevel object**
| Field | Type | Notes |
|---|---|---|
| `product_id` | string (UUID) | — |
| `product_name` | string | — |
| `product_unit` | string | — |
| `product_category` | string | `simple` \| `refillable` |
| `location_id` | string (UUID) | — |
| `location_name` | string | — |
| `quantity_filled` | number \| null | Filled containers in stock; `null` for simple products |
| `quantity_empty` | number \| null | Empty containers in stock; `null` for simple products |
| `quantity_total` | number \| null | Total units; `null` for refillable products (use filled/empty instead) |

---

### GET /api/stock/movements
**Auth**: All roles

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `date` | string (YYYY-MM-DD) | ❌ | WIB date filter; defaults to today WIB. Ignored when `start_date` and `end_date` are both supplied |
| `start_date` | string (YYYY-MM-DD) | ❌ | Inclusive range start (FR-DSH-012 detail modal). Takes effect only together with `end_date` |
| `end_date` | string (YYYY-MM-DD) | ❌ | Inclusive range end. Returns `400` when earlier than `start_date` |
| `product_id` | string (UUID) | ❌ | Restricts the result to a single product |

**Response `200`** — array of StockMovement objects (sorted newest first).

**StockMovement object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `product_id` | string (UUID) | — |
| `product_name` | string | — |
| `movement_type` | string | `receive` \| `transfer` \| `dispatch` \| `defect` \| `production` \| `adjustment` |
| `container_status` | string \| null | `filled` \| `empty` \| `na`; null for simple products |
| `quantity` | number | Always positive |
| `from_location_id` | string (UUID) \| null | Source location; null for external receives |
| `from_location_name` | string \| null | — |
| `to_location_id` | string (UUID) \| null | Destination; null for dispatch/defect/external sends |
| `to_location_name` | string \| null | — |
| `purchase_cost` | number \| null | Cost paid to vendor; only present on receive movements from vendor |
| `note` | string \| null | — |
| `created_by_name` | string | — |
| `created_at` | string (ISO 8601) | — |
| `batch_id` | string (UUID) \| null | Groups all movements created in a single API call; used for atomic reversal |
| `is_reversed` | boolean | `true` when this movement has been cancelled by a reversal; excluded from purchase cost aggregation |
| `is_reversal` | boolean | `true` when this movement is a compensating correction entry created by a reversal |
| `container_loan_id` | string (UUID) \| null | FK to ContainerLoan; set when this movement was created by a container loan operation (`POST /api/container-loans/bulk`) |

> ⚠️ **Known gap #6 (resolved)**: Frontend `StockMovement.note` now matches backend `note` field.

---

### POST /api/stock/movements
**Auth**: Owner only  
_(Single receive or defect record)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `product_id` | string (UUID) | ✅ | — |
| `movement_type` | string | ✅ | `receive` \| `defect` |
| `container_status` | string | Conditional | `filled` \| `empty` \| `na`; required for refillable products; for `defect` on refillable, only `filled` is accepted |
| `quantity` | number | ✅ | Positive integer |
| `from_location_id` | string (UUID) | ❌ | Source location; null for external vendor receives |
| `to_location_id` | string (UUID) | Conditional | Destination; required for `receive` |
| `purchase_cost` | number | Conditional | Required on vendor `receive` movements |
| `note` | string | Conditional | Required for `defect`; max 255 chars |

**Validation**
- `defect` on a refillable product with `container_status: empty` → `400 "Untuk produk refillable, defek hanya berlaku untuk kontainer terisi."`

**Behavior — `defect` on refillable product (filled)**  
Server atomically creates **two** `StockMovement` records sharing the same `batch_id`:
1. `from_location_id = <location>`, `container_status = filled` — filled container leaves the location
2. `to_location_id = <location>`, `container_status = empty` — empty container arrives at the same location

**Response `201`** — StockMovement object (or array of two for refillable defect).

---

### POST /api/stock/movements/bulk
**Auth**: Owner only  
_(Receive multiple products into the same destination atomically)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `movement_type` | string | ✅ | `receive` |
| `to_location_id` | string (UUID) | ✅ | Destination location |
| `note` | string | ❌ | max 255 chars |
| `items` | array | ✅ | At least 1 item |
| `items[].product_id` | string (UUID) | ✅ | — |
| `items[].container_status` | string | Conditional | `filled` \| `empty` \| `na`; required for refillable |
| `items[].quantity` | number | ✅ | Positive integer |
| `items[].purchase_cost` | number | Conditional | Required for vendor receives |

**Response `201`** — array of StockMovement objects (one per item).

---

### POST /api/stock/transfer
**Auth**: Owner or Kurir  
_(Single product transfer between locations — warehouse ↔ truck loading/return)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `product_id` | string (UUID) | ✅ | — |
| `container_status` | string | Conditional | `filled` \| `empty`; required for refillable products |
| `quantity` | number | ✅ | Positive integer |
| `from_location_id` | string (UUID) | ✅ | Source location |
| `to_location_id` | string (UUID) | ✅ | Destination location |
| `note` | string | ❌ | max 255 chars |

**Response `201`** — StockMovement object.

---

### POST /api/stock/transfer/bulk
**Auth**: Owner or Kurir  
_(Transfer multiple products between the same source/destination atomically)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `from_location_id` | string (UUID) | ✅ | Source location |
| `to_location_id` | string (UUID) | ✅ | Destination location |
| `note` | string | ❌ | max 255 chars |
| `items` | array | ✅ | At least 1 item |
| `items[].product_id` | string (UUID) | ✅ | — |
| `items[].container_status` | string | Conditional | `filled` \| `empty`; required for refillable |
| `items[].quantity` | number | ✅ | Positive integer |

**Response `201`** — array of StockMovement objects (one per item).

> ⚠️ **Stock levels**: This endpoint does not enforce minimum stock. Transfers that would result in negative stock at the source location are accepted by the API. The frontend displays a soft warning before submitting (see FR-STK-015 in FRD.md).

---

### POST /api/stock/vendor-exchange
**Auth**: Owner or Kurir  
_(Atomic: empties out → vendor, filled stock in ← vendor, purchase cost recorded)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `location_id` | string (UUID) | ✅ | Location where the exchange happens |
| `product_id` | string (UUID) | ✅ | — |
| `empty_quantity` | number | ✅ | Number of empty containers sent to vendor |
| `filled_quantity` | number | ✅ | Number of filled containers received from vendor |
| `purchase_cost` | number | ✅ | Total cost paid to vendor |
| `note` | string | ❌ | max 255 chars |

**Response `201`** — array of exactly 2 StockMovement objects: `[transfer_out, receive_in]`.

> ✅ **Known gap #4 (resolved)**: Backend `VendorExchangeRequest` fields renamed to `empty_quantity`/`filled_quantity`, matching the bulk endpoint and the frontend.

---

### POST /api/stock/vendor-exchange/bulk
**Auth**: Owner or Kurir  
_(Vendor exchange for multiple products in one atomic operation)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `location_id` | string (UUID) | ✅ | Location where exchanges happen |
| `note` | string | ❌ | max 255 chars |
| `items` | array | ✅ | At least 1 item |
| `items[].product_id` | string (UUID) | ✅ | — |
| `items[].empty_quantity` | number | ✅ | Empty containers sent to vendor |
| `items[].filled_quantity` | number | ✅ | Filled containers received from vendor |
| `items[].purchase_cost` | number | ✅ | Cost for this product's exchange |

**Response `201`** — array of StockMovement objects (2 per item: transfer_out + receive_in).

---

### POST /api/stock/production
**Auth**: Owner or Kasir  
_(In-house refill: atomically decrements empty stock and increments filled stock)_

> Product must have `category = refillable` and `production_type = selfproduced`.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `product_id` | string (UUID) | ✅ | Must be `refillable` and `selfproduced` |
| `location_id` | string (UUID) | ✅ | Location where production occurs |
| `quantity` | number | ✅ | Number of containers to refill; positive integer |
| `production_cost` | number | ❌ | Total cost of this production run |
| `note` | string | ❌ | max 255 chars |

**Response `201`** — array of 2 StockMovement objects: `[empty_out, filled_in]`.

---

### POST /api/stock/movements/{id}/reverse
**Auth**: Owner only  
_(Cancel a movement batch atomically; reverses all movements sharing the same `batch_id`)_

**Path Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `id` | string (UUID) | ✅ | ID of any movement in the batch to reverse |

**Rules:**
- `movement_type = dispatch` cannot be reversed via this endpoint (use transaction cancellation)
- `is_reversed = true` or `is_reversal = true` movements cannot be reversed again
- All movements sharing the same `batch_id` are reversed together atomically
- Linked `ContainerLoan` records (referenced by `container_loan_id` in the batch) are also reversed: marked `is_reversed = true` and excluded from `GET /api/container-loans` results

**Response `200`** — array of newly created StockMovement objects (the compensating movements), each with `is_reversal = true`.

**Response `400`** — `{ "message": "..." }` if movement not found, already reversed, or is a dispatch.

---

### POST /api/stock/adjustment
**Auth**: Owner only  
_(Create a manual stock adjustment to reconcile physical vs system stock)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `location_id` | string (UUID) | ✅ | Location being adjusted |
| `product_id` | string (UUID) | ✅ | Product being adjusted |
| `adjustment_quantity` | number | ✅ | Signed integer; positive = add stock, negative = remove stock; cannot be 0 |
| `container_status` | string | ❌ | Required for refillable products: `filled` or `empty` |
| `note` | string | ✅ | Reason for adjustment; required; max 255 chars |

**Movement type stored:** `adjustment`

**Response `200`** — `{ "message": "Penyesuaian stok berhasil." }`

**Response `400`** — `{ "message": "..." }` if validation fails.

---

### POST /api/stock/adjustment/bulk
**Auth**: Owner only  
_(Create a bulk manual stock adjustment — multiple products in a single operation; all share one BatchId for atomic reversal)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `location_id` | string (UUID) | ✅ | Location being adjusted (shared) |
| `note` | string | ✅ | Reason for adjustment; required; max 255 chars (shared) |
| `items` | array | ✅ | One or more adjustment items |
| `items[].product_id` | string (UUID) | ✅ | Product being adjusted |
| `items[].adjustment_quantity` | number | ✅ | Signed integer; positive = add stock, negative = remove stock; cannot be 0 |
| `items[].container_status` | string | ❌ | Required for refillable products: `filled` or `empty` |

**Response `200`** — `{ "message": "Penyesuaian stok berhasil dicatat." }`

**Response `400`** — `{ "message": "..." }` if validation fails.

---

## 8. Assignments

### GET /api/assignments
**Auth**: All roles  
_(Owner/Kasir see all assignments; Kurir sees only their own)_

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `date` | string (YYYY-MM-DD) | ❌ | WIB date filter; omit for all dates |

**Response `200`** — array of Assignment objects (sorted newest first).

**Assignment object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `status` | string | `pending` \| `fulfilled` \| `cancelled` |
| `kurir_id` | string (UUID) | — |
| `kurir_name` | string | — |
| `customer_id` | string (UUID) | — |
| `customer_name` | string | — |
| `location_id` | string (UUID) \| null | Stock source location chosen at creation |
| `location_name` | string \| null | — |
| `notes` | string \| null | — |
| `transaction_id` | string (UUID) \| null | Set after fulfillment |
| `created_at` | string (ISO 8601) | — |
| `items` | array | Planned delivery items |
| `items[].product_id` | string (UUID) | — |
| `items[].product_name` | string | — |
| `items[].product_unit` | string | — |
| `items[].quantity` | number | — |
| `items[].unit_price` | number | — |

---

### POST /api/assignments
**Auth**: Owner or Kasir

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `kurir_id` | string (UUID) | ✅ | Must be an active user with `role = kurir` |
| `customer_id` | string (UUID) | ✅ | — |
| `location_id` | string (UUID) | ✅ | Any active location (warehouse or vehicle); used as stock source when fulfilled |
| `notes` | string | ❌ | max 255 chars |
| `items` | array | ✅ | At least 1 item |
| `items[].product_id` | string (UUID) | ✅ | — |
| `items[].quantity` | number | ✅ | Positive integer |
| `items[].unit_price` | number | ✅ | Price snapshot |

**Response `201`** — Assignment object.

---

### POST /api/assignments/{id}/fulfill
**Auth**: Owner or Kurir (kurir can only fulfill own assignments)

**Path Params**: `id` — UUID of the assignment.

> Atomically: uses the delivered `items` (falling back to the assignment's stored items if omitted) to create a `delivery` Transaction, dispatches stock from the assignment's location, creates ContainerLoans (refillable + customer), creates a DebtPayment if `debt_payment_amount > 0`. Marks assignment `fulfilled`.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `paid_amount` | number | ✅ | Amount collected; ≥ 0 |
| `payment_method` | string | ❌ | `cash` \| `transfer` \| `qris`; defaults to `cash` |
| `notes` | string | ❌ | Required when delivered quantities differ from the assignment; max 255 chars |
| `items` | array | ❌ | Actual items delivered. When provided, overrides the assignment's stored items (allows partial delivery). When omitted, the assignment's original items are used. |
| `items[].product_id` | string (UUID) | ✅ | — |
| `items[].quantity` | number | ✅ | Positive integer |
| `items[].unit_price` | number | ✅ | Price snapshot |
| `container_returns` | array | ❌ | Empty containers returned by customer at delivery |
| `container_returns[].product_id` | string (UUID) | ✅ | — |
| `container_returns[].quantity` | number | ✅ | Positive integer |
| `debt_payment_amount` | number | ❌ | Settle pre-existing debt alongside this transaction |

**Response `204`** — No content.

---

### PUT /api/assignments/{id}/cancel
**Auth**: Owner or Kasir

**Path Params**: `id` — UUID of the assignment.

No request body.

**Response `204`** — No content.

---

## 9. Transactions

### GET /api/transactions
**Auth**: All roles  
_(Owner sees all; Kurir and Kasir see only their own transactions)_

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `date` | string (YYYY-MM-DD) | ❌ | WIB date filter; defaults to today WIB |

**Response `200`** — array of Transaction objects (sorted newest first).

**Transaction object (list)**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `transaction_type` | string | `delivery` \| `counter` |
| `customer_id` | string (UUID) \| null | — |
| `customer_name` | string \| null | — |
| `staff_id` | string (UUID) | The user who created the transaction |
| `staff_name` | string | — |
| `location_id` | string (UUID) \| null | Source location |
| `location_name` | string \| null | — |
| `status` | string | `completed` \| `cancelled` |
| `payment_method` | string \| null | `cash` \| `transfer` \| `qris` |
| `total_amount` | number | Sum of all item subtotals |
| `paid_amount` | number | Amount collected |
| `debt_amount` | number | `total_amount - paid_amount`; stored for query performance |
| `notes` | string \| null | — |
| `created_at` | string (ISO 8601) | — |
| `items` | array | — |
| `items[].product_id` | string (UUID) | — |
| `items[].product_name` | string | — |
| `items[].quantity` | number | — |
| `items[].unit_price` | number | Price snapshot at time of sale |

> ⚠️ **Known gap #3 (resolved)**: Frontend `Transaction` type now uses `transaction_type`, `staff_id`, and `staff_name` matching the backend response.

---

### POST /api/transactions
**Auth**: All roles

> Server atomically creates: Transaction + TransactionItems + dispatch StockMovement(s) + ContainerLoans (if refillable + customer) + receive StockMovements for `container_returns` + DebtPayment if `debt_payment_amount > 0`.  
> `status` is always set to `completed` on creation.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `transaction_type` | string | ✅ | `delivery` \| `counter` |
| `customer_id` | string (UUID) | Conditional | Required for `delivery` |
| `location_id` | string (UUID) | ✅ | Truck for delivery; warehouse for counter |
| `items` | array | ✅ | At least 1 item |
| `items[].product_id` | string (UUID) | ✅ | — |
| `items[].quantity` | number | ✅ | Positive integer |
| `items[].unit_price` | number | ✅ | Price snapshot |
| `paid_amount` | number | ✅ | Amount collected; ≥ 0 |
| `payment_method` | string | ✅ | `cash` \| `transfer` \| `qris` |
| `notes` | string | ❌ | — |
| `container_returns` | array | ❌ | Empty containers returned by customer |
| `container_returns[].product_id` | string (UUID) | ✅ | — |
| `container_returns[].quantity` | number | ✅ | Positive integer |
| `debt_payment_amount` | number | ❌ | Settle pre-existing debt; requires `customer_id` |

**Response `201`** — Transaction object (list shape, as documented above).

> ⚠️ **Stock levels**: This endpoint does not enforce minimum stock. Transactions that would result in negative stock at the source location are accepted by the API. The frontend displays a soft warning before submitting (see FR-TXN-021 in FRD.md).

---

### GET /api/transactions/{id}
**Auth**: All roles (Kurir/Kasir can only access own transactions)

**Path Params**: `id` — UUID of the transaction.

**Response `200`** — Transaction detail object (extends the list object with additional fields).

**Additional fields on detail response**
| Field | Type | Notes |
|---|---|---|
| `items[].subtotal` | number | `quantity × unit_price` |
| `payments` | array | All payment events recorded on this transaction |
| `payments[].id` | string (UUID) | — |
| `payments[].amount` | number | — |
| `payments[].method` | string | `cash` \| `transfer` \| `qris` |
| `payments[].reference_no` | string \| null | Payment gateway reference |
| `payments[].paid_at` | string (ISO 8601) | — |
| `container_returns` | array | Containers returned by customer at this transaction |
| `container_returns[].product_id` | string (UUID) | — |
| `container_returns[].product_name` | string | — |
| `container_returns[].quantity` | number | — |

---

### PUT /api/transactions/{id}/status
**Auth**: All roles (Owner can cancel any; Kurir/Kasir can cancel own only)

**Path Params**: `id` — UUID of the transaction.

> Server creates compensating receive StockMovements and reverses ContainerLoans for all items.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `status` | string | ✅ | Only valid value: `cancelled` |

**Response `204`** — No content.

---

### POST /api/transactions/{id}/payments
**Auth**: Owner only

**Path Params**: `id` — UUID of the transaction.

Records a partial or full payment against an existing transaction.

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `amount` | number | ✅ | Positive; > 0 |
| `method` | string | ✅ | `cash` \| `transfer` \| `qris` |
| `reference_no` | string | ❌ | Payment gateway reference |

**Response `201`**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `transaction_id` | string (UUID) | — |
| `amount` | number | — |
| `method` | string | — |
| `reference_no` | string \| null | — |
| `paid_at` | string (ISO 8601) | — |

---

## 10. Container Loans

### GET /api/container-loans
**Auth**: Owner only

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `customer_id` | string (UUID) | ❌ | Filter by customer; omit for all loans |

Returns the raw event log (not aggregated). Each record represents a single loan or return event. Loans with `is_reversed = true` are excluded from the response.

**Response `200`** — array of ContainerLoan objects.

**ContainerLoan object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `transaction_id` | string (UUID) \| null | Source transaction; null for standalone loan records |
| `customer_id` | string (UUID) | — |
| `customer_name` | string | — |
| `product_id` | string (UUID) | — |
| `product_name` | string | — |
| `product_unit` | string | — |
| `quantity` | number | Positive = lent to customer; negative = returned by customer |
| `note` | string \| null | Optional note supplied at creation |
| `is_reversed` | boolean | `true` when this loan record has been reversed via stock movement reversal (`POST /api/stock/movements/{id}/reverse`) |
| `created_by_name` | string | — |
| `created_at` | string (ISO 8601) | — |

---

### POST /api/container-loans
**Auth**: Owner only

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `customer_id` | string (UUID) | ✅ | — |
| `product_id` | string (UUID) | ✅ | Must be `category = refillable` |
| `quantity` | number | ✅ | Positive to lend; negative to record a return |
| `notes` | string | ❌ | Optional note for this loan record |

**Response `201`** — ContainerLoan object.

> ✅ **Known gap #3 (resolved)**: `notes` field added to `CreateContainerLoanRequest` and `note` exposed in `ContainerLoanResponse`. Field is now persisted and returned.

---

### POST /api/container-loans/bulk
**Auth**: Owner only  
_(Record multiple container loans or returns for one customer without a transaction. For each item, a `StockMovement` (type `adjustment`) is also created and linked back to the loan via `container_loan_id`.)_

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `customer_id` | string (UUID) | ✅ | Must be an active customer |
| `items` | array | ✅ | At least one item required |
| `items[].product_id` | string (UUID) | ✅ | Must be `category = refillable` |
| `items[].quantity` | number | ✅ | Positive = lend to customer; negative = receive from customer; cannot be 0 |
| `items[].note` | string | ❌ | Per-item note (overrides shared note for this item) |
| `note` | string | ❌ | Shared note applied to all items |

**Response `200`** — array of ContainerLoan objects (one per item).

**Response `400`** — `{ "message": "..." }` if customer inactive, any product not refillable, or any quantity is 0.

---

## 11. Debt Payments

### GET /api/debt-payments
**Auth**: Owner or Kasir

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `date` | string (YYYY-MM-DD) | ❌ | WIB date filter; omit for all records |

**Response `200`** — array of DebtPayment objects (sorted newest first).

**DebtPayment object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `customer_id` | string (UUID) | — |
| `customer_name` | string | — |
| `amount` | number | — |
| `method` | string | `cash` \| `transfer` \| `qris` |
| `reference_no` | string \| null | — |
| `note` | string \| null | — |
| `created_by_name` | string | — |
| `created_at` | string (ISO 8601) | — |

> ⚠️ **Known gap #6 (resolved)**: Frontend `DebtPayment.note` now matches backend `note` field.

---

### POST /api/debt-payments
**Auth**: Owner only

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `customer_id` | string (UUID) | ✅ | — |
| `amount` | number | ✅ | Positive; > 0 |
| `method` | string | ✅ | `cash` \| `transfer` \| `qris` |
| `reference_no` | string | ❌ | Payment reference number |
| `note` | string | ❌ | — |

**Response `201`** — DebtPayment object.

> ⚠️ **Known gap #8 (resolved)**: Frontend `debtService.create()` now sends required `method` field (`cash`/`transfer`/`qris`) and `DebtPayment` type includes `method` and `reference_no`.

---

## 12. Cash Flow

### GET /api/cash-flow
**Auth**: Owner only

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `date` | string (YYYY-MM-DD) | ❌ | Single WIB date filter; defaults to today WIB. Ignored when `start_date` + `end_date` are both provided. |
| `start_date` | string (YYYY-MM-DD) | ❌ | Range start (WIB). Must be paired with `end_date`. |
| `end_date` | string (YYYY-MM-DD) | ❌ | Range end (WIB, inclusive). Must be paired with `start_date`. |

> When both `start_date` and `end_date` are present, the response aggregates all entries across the full date range (e.g. an entire calendar month). The `date` param is ignored in this case.

> **Operational expenses (FR-CSH-006)** are a fourth entry source. Unlike the other three sources (which are filtered by `created_at`), expenses are filtered by their business date `expense_date`: a single `date` matches `expense_date = date`, and a range matches `expense_date BETWEEN start_date AND end_date`. The entry `created_at` is emitted as the expense business date combined with the recording time-of-day (WIB), so back-dated expenses group under the correct day.

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `total_cash_in` | number | Sum of all `cash_in` entries |
| `total_cash_out` | number | Sum of all `cash_out` entries |
| `net_cash` | number | `total_cash_in - total_cash_out` |
| `total_new_debt` | number | Sum of new debt created (tracked separately from cash) |
| `entries` | array | All entries for the date; sorted newest first |
| `entries[].id` | string | Synthetic composite ID |
| `entries[].flow_type` | string | `cash_in` \| `cash_out` \| `new_debt` |
| `entries[].category` | string | `sale_payment` \| `debt_payment` \| `stock_purchase` \| `debt_created` \| `operational_expense` |
| `entries[].amount` | number | — |
| `entries[].description` | string | Human-readable label |
| `entries[].reference_id` | string \| null | ID of the source record |
| `entries[].created_by_name` | string | — |
| `entries[].created_at` | string (ISO 8601) | — |

**Entry source mapping**
| Source record | `flow_type` | `category` |
|---|---|---|
| Transaction with `paid_amount > 0` | `cash_in` | `sale_payment` |
| Transaction with `debt_amount > 0` | `new_debt` | `debt_created` |
| Standalone DebtPayment | `cash_in` | `debt_payment` |
| StockMovement with `purchase_cost > 0` | `cash_out` | `stock_purchase` |
| Expense (operational) | `cash_out` | `operational_expense` |

---

### GET /api/expenses
**Auth**: Owner only

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `date` | string (YYYY-MM-DD) | ❌ | Business date (WIB) filter applied to `expense_date`; defaults to today WIB |

**Response `200`** — array of Expense objects.

| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `category` | string | `fuel` \| `dues` \| `electricity` \| `gallon_cap` \| `cleaning` \| `salary` \| `other` |
| `description` | string | Free-text detail (max 255 chars) |
| `amount` | number | Positive |
| `expense_date` | string (YYYY-MM-DD) | Business date (WIB) — determines which day the expense appears under |
| `created_by_name` | string | — |
| `created_at` | string (ISO 8601) | — |

---

### POST /api/expenses
**Auth**: Owner only

**Request Body**
| Field | Type | Required | Notes |
|---|---|---|---|
| `category` | string | ✅ | One of `fuel` \| `dues` \| `electricity` \| `gallon_cap` \| `cleaning` \| `salary` \| `other` |
| `description` | string | ✅ | Free text, max 255 chars (trimmed) |
| `amount` | number | ✅ | Positive |
| `expense_date` | string (YYYY-MM-DD) | ✅ | Business date (WIB); must not be in the future |

**Response `200`** — Expense object.

**Errors**
| Status | Message |
|---|---|
| `400` | `"Kategori pengeluaran wajib dipilih."` / `"Kategori pengeluaran tidak valid."` |
| `400` | `"Deskripsi pengeluaran wajib diisi."` / `"Deskripsi pengeluaran tidak boleh lebih dari 255 karakter."` |
| `400` | `"Jumlah pengeluaran harus berupa angka positif."` |
| `400` | `"Tanggal pengeluaran tidak boleh di masa depan."` |

---

### PUT /api/expenses/{id}
**Auth**: Owner only

**Request Body** — identical to `POST /api/expenses`.

**Response `200`** — updated Expense object.

**Errors** — `404` (`"Pengeluaran tidak ditemukan."`) when the expense does not exist; otherwise the same `400` validations as `POST`.

---

### DELETE /api/expenses/{id}
**Auth**: Owner only

**Response `204`** — no body.

**Errors** — `404` (`"Pengeluaran tidak ditemukan."`) when the expense does not exist.

---

## 13. Dashboard

### GET /api/dashboard
**Auth**: All authenticated roles (owner, kasir, kurir). For kasir/kurir, transaction-derived stats are scoped to the caller's own records; `staff_revenue` is always empty.

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `date` | string (YYYY-MM-DD) | ❌ | Selected date for day-specific stats; defaults to today WIB. Does not affect the "Pergerakan Stok" section, which has its own period endpoint (FR-DSH-012) |

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `today_revenue` | number | Total `paid_amount` from caller's completed transactions on `date` (all roles for owner; own transactions for kasir/kurir) |
| `today_transactions` | number | Count of caller's completed transactions on `date` |
| `today_purchase_cost` | number | Total `purchase_cost` from stock movements on `date` (owner: all; kasir/kurir: own movements) |
| `today_debt_collected` | number | Total standalone debt payments received on `date` (owner: all; kasir/kurir: own) |
| `low_stock_count` | number | Count of product/location combinations below threshold (store-wide, all roles) |
| `total_outstanding_debt` | number | Aggregate outstanding debt across all customers (store-wide, all roles; current state, not date-filtered) |
| `previous_day_revenue` | number | Total revenue from the day before `date` (scoped same as `today_revenue`) |
| `weekly_chart` | array | 7 entries; last 7 days ending on `date` (index 6 = `date`); revenue/transaction_count scoped to caller for kasir/kurir |
| `weekly_chart[].date` | string (YYYY-MM-DD) | — |
| `weekly_chart[].revenue` | number | — |
| `weekly_chart[].transaction_count` | number | — |
| `weekly_chart[].purchase_cost` | number | — |
| `recent_transactions` | array | Transactions on `date`; sorted newest first; scoped to caller for kasir/kurir |
| `recent_transactions[].id` | string (UUID) | — |
| `recent_transactions[].created_at` | string (ISO 8601) | — |
| `recent_transactions[].customer_name` | string \| null | — |
| `recent_transactions[].created_by_name` | string | — |
| `recent_transactions[].type` | string | `delivery` \| `counter` |
| `recent_transactions[].total_amount` | number | — |
| `recent_transactions[].paid_amount` | number | — |
| `recent_transactions[].status` | string | `completed` \| `cancelled` |
| `warehouse_stock` | array | Current stock levels for the warehouse location (not date-filtered); same shape as StockLevel object |
| `customer_debts` | array | Active customers with `outstanding_debt > 0`; sorted by debt descending (not date-filtered) |
| `customer_debts[].customer_id` | string (UUID) | — |
| `customer_debts[].customer_name` | string | — |
| `customer_debts[].outstanding_debt` | number | — |
| `container_loans` | array | Net container balance per customer + product (`Σ container_loans.quantity`); excludes reversed loans (`is_reversed=true`); active customers only; pairs with a net of `0` omitted; sorted by customer name (not date-filtered, store-wide for all roles) |
| `container_loans[].customer_id` | string (UUID) | — |
| `container_loans[].customer_name` | string | — |
| `container_loans[].product_id` | string (UUID) | — |
| `container_loans[].product_name` | string | — |
| `container_loans[].product_unit` | string | — |
| `container_loans[].net_quantity` | number | Positive = customer still holds our containers; negative = we hold the customer's containers (owed back as filled containers on the next delivery) |
| `staff_revenue` | array | Revenue breakdown per staff member for completed transactions on `date`; sorted by revenue descending |
| `staff_revenue[].staff_id` | string (UUID) | — |
| `staff_revenue[].staff_name` | string | — |
| `staff_revenue[].revenue` | number | Sum of `paid_amount` for completed transactions created by this staff member on `date` |
| `staff_revenue[].transaction_count` | number | Count of completed transactions created by this staff member on `date` |
| `payment_method_breakdown` | array | Revenue breakdown by payment method for completed transactions on `date`. Scoped to caller for kasir/kurir; store-wide for owner. Always contains exactly 3 items (cash, transfer, qris), ordered in that sequence. Items with zero transactions still appear with `amount=0` and `count=0`. Each item also carries a nested `staff` array (FR-DSH-015) |
| `payment_method_breakdown[].method` | string | `cash` \| `transfer` \| `qris` |
| `payment_method_breakdown[].label` | string | `Tunai` \| `Transfer` \| `QRIS` (localized label) |
| `payment_method_breakdown[].amount` | number | Sum of `paid_amount` for completed transactions of this payment method on `date` |
| `payment_method_breakdown[].count` | number | Count of completed transactions of this payment method on `date` |
| `payment_method_breakdown[].staff` | array | Per-staff income breakdown for this payment method (FR-DSH-015). **Owner only** — always an empty array for kasir/kurir. Sorted by `amount` descending, then `staff_name` ascending |
| `payment_method_breakdown[].staff[].staff_id` | string (UUID) | — |
| `payment_method_breakdown[].staff[].staff_name` | string | — |
| `payment_method_breakdown[].staff[].amount` | number | Sum of `paid_amount` for completed transactions of this payment method created by this staff member on `date` |
| `payment_method_breakdown[].staff[].count` | number | Count of completed transactions of this payment method created by this staff member on `date` |

---

### GET /api/dashboard/stock-summary
**Auth**: All authenticated roles (owner, kasir, kurir). Store-wide — not user-scoped.

FR-DSH-012 — the "Pergerakan Stok" section's own period filter. Every other dashboard section keeps using `GET /api/dashboard` with its single `date` filter, so changing the period never refetches (or alters) the rest of the dashboard.

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `period` | string | ❌ | `day` \| `week` \| `month` \| `year` \| `custom`; defaults to `day`, or to `custom` when both range bounds are supplied. Any other value → `400` |
| `date` | string (YYYY-MM-DD) | ❌ | Anchor date for `day`, `week`, `month` and `year`; defaults to today WIB. Ignored for `custom` |
| `start_date` | string (YYYY-MM-DD) | ✅ for `custom` | Inclusive range start |
| `end_date` | string (YYYY-MM-DD) | ✅ for `custom` | Inclusive range end; must be on or after `start_date` and not in the future |

**Resolved ranges (WIB, inclusive)**
| `period` | Range |
|---|---|
| `day` | `date` |
| `week` | Calendar week, Monday–Sunday, containing `date` |
| `month` | Calendar month containing `date` (1st → last day) |
| `year` | Calendar year containing `date` (1 Jan → 31 Dec) |
| `custom` | `start_date` through `end_date` |

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `period` | string | Echo of the resolved period: `day` \| `week` \| `month` \| `year` \| `custom` |
| `start_date` | string (YYYY-MM-DD) | Resolved inclusive range start |
| `end_date` | string (YYYY-MM-DD) | Resolved inclusive range end |
| `items` | array | One entry per product with activity in the range; sorted alphabetically by `product_name`; empty when there is none |
| `items[].product_id` | string (UUID) | — |
| `items[].product_name` | string | — |
| `items[].product_unit` | string | — |
| `items[].product_category` | string | `simple` \| `refillable` |
| `items[].total_sold` | number | Units sold via dispatch. Refillable: filled-container dispatch qty only. Simple: all dispatch qty |
| `items[].total_received` | number | Units received inbound (`to_location_id != null && from_location_id == null`). Refillable: filled-container inbound qty only. Simple: all inbound qty |

Cancelled movements (`is_reversed=true` or `is_reversal=true`) are excluded, and products with zero activity in both directions are omitted.

**Response `400`** — `{ message }` with one of: `"Periode tidak valid."`, `"Tanggal mulai dan tanggal selesai wajib diisi."`, `"Tanggal mulai harus sebelum atau sama dengan tanggal selesai."`, `"Tanggal tidak boleh di masa depan."`

---

## 14. Health

### GET /api/health
**Auth**: Anonymous

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `status` | string | `healthy` |
| `version` | string | App (release) version running on the server — sourced from `<Version>` in `Pos.Api.csproj` (e.g. `1.0.0`) |
| `timestamp` | string (ISO 8601) | Server timestamp |

---

## 15. Settlements (Tutup Kas)

Settlement harian per pengguna — FR-STL. Semua endpoint memerlukan autentikasi (`AllStaff`) kecuali yang
bertanda **OwnerOnly**.

### `GET /api/settlements/status`
Status pemblokiran pemanggil; dipakai frontend untuk banner.

```json
{ "blocked": true, "blocking_business_date": "2026-09-17", "blocking_status": "submitted", "blocking_settlement_id": "...", "message": "Settlement 17 September 2026 sedang menunggu persetujuan owner." }
```

### `GET /api/settlements/preview?date=YYYY-MM-DD`
Angka hidup untuk tanggal tersebut (default hari ini WIB). `404` bila tanggal di masa depan.
Mengembalikan `business_date`, `status`, `settlement_id`, `expected_cash`, `counted_cash`, `cash_variance`,
`cash_in`, `cash_out`, `cash_adjustments`, `transfer_expected`, `qris_expected`, `new_debt_total`,
`debt_payment_total`, `vehicle_location_id`, `vehicle_location_name`, `methods[]`, `stocks[]`.

### `GET /api/settlements?from=&to=&user_id=&status=`
Daftar settlement. Non-owner hanya melihat miliknya; `user_id` hanya berlaku untuk owner.

### `GET /api/settlements/{id}`
Detail + baris metode + baris stok + jejak audit. Non-owner hanya boleh membaca miliknya (`404` bila bukan).

### `POST /api/settlements/submit`

| Field | Tipe | Wajib | Catatan |
|---|---|---|---|
| `business_date` | string (`yyyy-MM-dd`) | ❌ | Default hari ini (WIB) |
| `counted_cash` | number | ✅ | Kas fisik yang dihitung |
| `method_lines` | array `{ method, counted_amount }` | ❌ | Metode non-tunai default ke nilai sistem |
| `stock_lines` | array `{ product_id, counted_filled, counted_empty }` | ❌ | Item yang tidak dikirim default ke nilai sistem |
| `note` | string | ❌ | Maks 500 |

`200` dengan objek settlement · `409` `{ message, code }` dengan `code` = `CASH_VARIANCE`, `DAY_LOCKED`, atau
`NO_ACTIVITY` · `400` untuk validasi lain.

### `POST /api/settlements/{id}/approve` **OwnerOnly**
Setujui settlement berstatus `submitted`. Hari menjadi terkunci (Gerbang B). `400` bila status bukan `submitted`.

### `POST /api/settlements/{id}/reject` **OwnerOnly**

| Field | Tipe | Wajib |
|---|---|---|
| `reason` | string | ✅ |

Mengembalikan hari ke `rejected`; pengguna kembali diblokir Gerbang A.

### `POST /api/settlements/{id}/reopen` **OwnerOnly**

| Field | Tipe | Wajib |
|---|---|---|
| `reason` | string | ✅ |

Membuka hari `approved` menjadi `open`.

### `POST /api/settlements/adjustments` **OwnerOnly**

| Field | Tipe | Wajib | Catatan |
|---|---|---|---|
| `user_id` | uuid | ✅ | Pengguna yang kasnya dikoreksi |
| `business_date` | string (`yyyy-MM-dd`) | ✅ | Tidak boleh di masa depan; ditolak bila hari sudah `approved` |
| `amount` | number | ✅ | Bertanda: negatif = kas kurang, positif = kas lebih; tidak boleh 0 |
| `reason` | string | ✅ | Maks 255 |

### `PUT /api/transactions/{id}`
Perbaikan transaksi sebelum settlement (FR-STL-008).

| Field | Tipe | Wajib |
|---|---|---|
| `items` | array `{ product_id, quantity, unit_price }` | ✅ |
| `paid_amount` | number | ✅ |
| `payment_method` | string (`cash`/`transfer`/`qris`) | ✅ |
| `reference_no` | string | ❌ |
| `notes` | string | ❌ |
| `reason` | string | ✅ |

`400` dengan pesan kesalahan bila tidak berhak, hari sudah `approved`, atau transaksi memiliki lebih dari satu
pembayaran sementara `paid_amount` diubah.

### `POST /api/transactions/{id}/payments`
Berubah dari **OwnerOnly** menjadi berbasis kepemilikan: owner boleh mencatat pada transaksi apa pun,
kurir/kasir hanya pada transaksi miliknya (`staff_id == user id`). Pembayaran lama tetap boleh dicatat saat
pengguna terkena Gerbang A — menagih hutang lama adalah perbaikan, bukan pekerjaan baru.

## Known Gaps

All previously identified gaps have been resolved. No outstanding mismatches between the frontend service layer and the backend DTOs.

---

### Gap 1 ✅ — `GET /api/customers`: Missing `outstanding_debt` in response
**Resolved**: `decimal OutstandingDebt` added to `CustomerResponse`. Computed as `SUM(transactions.debt_amount) - SUM(debt_payments.amount)` per customer in a batched query inside `CustomerService.GetAllAsync`. Also computed in `GetByIdAsync` and `UpdateAsync`.

---

### Gap 2 ✅ — `POST /api/assignments/{id}/fulfill`: `items[]` missing from backend DTO
**Resolved**: Decision — items are always taken from the original saved assignment as-is. The backend `FulfillAssignmentRequest` intentionally has no `Items` property; the frontend no longer sends `items[]` in `FulfillAssignmentPayload`.

---

### Gap 3 ✅ — `POST /api/container-loans`: `notes` silently dropped
**Resolved**: `string? Notes` added to `CreateContainerLoanRequest`. `string? Note` added to `ContainerLoanResponse`. `ContainerLoanService.CreateAsync` now persists the note and returns it in the response. Migration `AddNoteToContainerLoan` applied.

---

### Gap 4 ✅ — `POST /api/stock/vendor-exchange`: field name mismatch
**Resolved**: `VendorExchangeRequest` properties renamed `QtyEmptyOut` → `EmptyQuantity` and `QtyFilledIn` → `FilledQuantity`. JSON keys are now `empty_quantity`/`filled_quantity`, matching both the bulk endpoint and the frontend.

---

*Total: 0 known gaps remaining. All 4 gaps resolved.*
