namespace Pos.Api.DTOs.Customers;

public record UpdateCustomerRequest(
    string Name,
    string? Phone,
    string? Address,
    bool IsActive,
    decimal? InitialDebt = null
);
