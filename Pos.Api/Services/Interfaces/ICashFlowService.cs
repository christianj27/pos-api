using Pos.Api.DTOs.CashFlow;

namespace Pos.Api.Services.Interfaces;

public interface ICashFlowService
{
    Task<CashFlowSummaryResponse> GetCashFlowAsync(DateOnly date);
}
