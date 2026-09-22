using Pos.Api.DTOs.Customers;

namespace Pos.Api.Services.Interfaces;

public interface ICustomerService
{
    /// <param name="activeOnly">
    /// Defaults to <c>true</c>: soft-deleted customers are excluded from the list unless
    /// <c>false</c> is passed explicitly.
    /// </param>
    Task<IEnumerable<CustomerResponse>> GetAllAsync(bool activeOnly = true, string? userRole = null);
    Task<CustomerResponse?> GetByIdAsync(Guid id);
    Task<(CustomerResponse? Customer, string? Error)> CreateAsync(CreateCustomerRequest request);
    Task<(CustomerResponse? Customer, string? Error)> UpdateAsync(Guid id, UpdateCustomerRequest request);
    /// <summary>Soft delete: sets <c>is_active = false</c>, retaining the record for history.</summary>
    Task<(bool Success, string? Error)> DeleteAsync(Guid id);
    Task<CustomerPricingResponse?> GetPricingAsync(Guid customerId);
    Task<(bool Success, string? Error)> UpdatePricingAsync(Guid customerId, UpdateCustomerPricingRequest request);
    Task<CustomerDebtSummaryResponse?> GetDebtAsync(Guid customerId);
    Task<ContainerLoanSummaryResponse?> GetContainerLoansAsync(Guid customerId);
    Task<CustomerDebtHistoryResponse?> GetDebtHistoryAsync(Guid customerId);

    /// <summary>
    /// FR-CST-011 — per-customer "Pergerakan Stok" summary (Terjual / Dikembalikan) for an inclusive
    /// WIB date range. Returns <c>null</c> when the customer does not exist.
    /// </summary>
    Task<CustomerStockSummaryResponse?> GetStockSummaryAsync(Guid customerId, string period, DateOnly rangeStart, DateOnly rangeEnd);
}
