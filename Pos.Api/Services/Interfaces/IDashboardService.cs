using Pos.Api.DTOs.Dashboard;

namespace Pos.Api.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardResponse> GetDashboardAsync(DateOnly date, Guid userId, string role);
}
