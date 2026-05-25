# API Contract — POS App
> MSMe Water & Gas | Version 1.0 | Last updated: May 26, 2026

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
| `is_active` | boolean | ❌ | Set `false` to deactivate customer |
| `initial_debt` | number | ❌ | Opening balance ≥ 0; replaces previous value when provided |
| `is_confidential` | boolean | ❌ | Owner only — ignored when sent by kasir/kurir |

**Response `200`** — updated Customer object.

> ⚠️ **Known gap #2 (resolved)**: Frontend `customerService.deactivate()` called `DELETE /api/customers/{id}` (no such route). Fixed: now calls `PUT /api/customers/{id}` with `{ is_active: false }`.

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
| `date` | string (YYYY-MM-DD) | ❌ | WIB date filter; defaults to today WIB |

**Response `200`** — array of StockMovement objects (sorted newest first).

**StockMovement object**
| Field | Type | Notes |
|---|---|---|
| `id` | string (UUID) | — |
| `product_id` | string (UUID) | — |
| `product_name` | string | — |
| `movement_type` | string | `receive` \| `transfer` \| `dispatch` \| `defect` \| `production` |
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
| `container_status` | string | Conditional | `filled` \| `empty` \| `na`; required for refillable products |
| `quantity` | number | ✅ | Positive integer |
| `from_location_id` | string (UUID) | ❌ | Source location; null for external vendor receives |
| `to_location_id` | string (UUID) | Conditional | Destination; required for `receive` |
| `purchase_cost` | number | Conditional | Required on vendor `receive` movements |
| `note` | string | Conditional | Required for `defect`; max 255 chars |

**Response `201`** — StockMovement object.

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

Returns the raw event log (not aggregated). Each record represents a single loan or return event.

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
| `entries[].category` | string | `sale_payment` \| `debt_payment` \| `stock_purchase` \| `debt_created` |
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

---

## 13. Dashboard

### GET /api/dashboard
**Auth**: All authenticated roles (owner, kasir, kurir). For kasir/kurir, transaction-derived stats are scoped to the caller's own records; `staff_revenue` is always empty.

**Query Params**
| Param | Type | Required | Notes |
|---|---|---|---|
| `date` | string (YYYY-MM-DD) | ❌ | Selected date for day-specific stats; defaults to today WIB |

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
| `staff_revenue` | array | Revenue breakdown per staff member for completed transactions on `date`; sorted by revenue descending |
| `staff_revenue[].staff_id` | string (UUID) | — |
| `staff_revenue[].staff_name` | string | — |
| `staff_revenue[].revenue` | number | Sum of `paid_amount` for completed transactions created by this staff member on `date` |
| `staff_revenue[].transaction_count` | number | Count of completed transactions created by this staff member on `date` |

---

## 14. Health

### GET /api/health
**Auth**: Anonymous

**Response `200`**
| Field | Type | Notes |
|---|---|---|
| `status` | string | `healthy` |
| `timestamp` | string (ISO 8601) | Server timestamp |

---

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
