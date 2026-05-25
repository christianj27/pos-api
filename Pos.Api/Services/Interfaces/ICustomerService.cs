using Pos.Api.DTOs.Customers;

namespace Pos.Api.Services.Interfaces;

public interface ICustomerService
{
    Task<IEnumerable<CustomerResponse>> GetAllAsync(bool activeOnly = false, string? userRole = null);
    Task<CustomerResponse?> GetByIdAsync(Guid id);
    Task<(CustomerResponse? Customer, string? Error)> CreateAsync(CreateCustomerRequest request);
    Task<(CustomerResponse? Customer, string? Error)> UpdateAsync(Guid id, UpdateCustomerRequest request);
    Task<CustomerPricingResponse?> GetPricingAsync(Guid customerId);
    Task<(bool Success, string? Error)> UpdatePricingAsync(Guid customerId, UpdateCustomerPricingRequest request);
    Task<CustomerDebtSummaryResponse?> GetDebtAsync(Guid customerId);
    Task<ContainerLoanSummaryResponse?> GetContainerLoansAsync(Guid customerId);
    Task<CustomerDebtHistoryResponse?> GetDebtHistoryAsync(Guid customerId);
}
