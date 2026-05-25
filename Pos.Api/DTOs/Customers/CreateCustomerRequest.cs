namespace Pos.Api.DTOs.Customers;

public record CreateCustomerRequest(
    string Name,
    string? Phone,
    string? Address,
    decimal? InitialDebt = null,
    bool? IsConfidential = null
);
