namespace Pos.Api.DTOs.Customers;

/// <summary>Per-staff split of a single product's movements for one customer (FR-CST-011).</summary>
public record CustomerStockStaffItem(
    Guid StaffId,
    string StaffName,
    int Sold,
    int Returned
);

/// <summary>One product row of the per-customer "Pergerakan Stok" summary (FR-CST-011).</summary>
public record CustomerStockProductItem(
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    string ProductCategory,
    int TotalSold,
    int TotalReturned,
    IEnumerable<CustomerStockStaffItem> Staff
);

/// <summary>
/// FR-CST-011 — per-customer "Pergerakan Stok" summary for a resolved period
/// (see <see cref="Services.StockPeriodRange"/>). Only movements belonging to the customer's own
/// transactions are counted, because <c>StockMovement</c> carries no customer column.
/// </summary>
public record CustomerStockSummaryResponse(
    Guid CustomerId,
    string CustomerName,
    string Period,
    string StartDate,
    string EndDate,
    IEnumerable<CustomerStockProductItem> Items
);
