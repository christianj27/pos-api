using Pos.Api.DTOs.ContainerLoans;

namespace Pos.Api.Services.Interfaces;

public interface IContainerLoanService
{
    Task<IEnumerable<ContainerLoanResponse>> GetAllAsync(Guid? customerId);
    Task<(ContainerLoanResponse? Loan, string? Error)> CreateAsync(CreateContainerLoanRequest request, Guid createdBy);
    Task<(IEnumerable<ContainerLoanResponse>? Loans, string? Error)> CreateBulkAsync(BulkCreateContainerLoanRequest request, Guid createdBy);
}
