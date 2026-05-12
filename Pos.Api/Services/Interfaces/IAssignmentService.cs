using Pos.Api.DTOs.Assignments;

namespace Pos.Api.Services.Interfaces;

public interface IAssignmentService
{
    Task<IEnumerable<AssignmentResponse>> GetAllAsync(Guid userId, string role);
    Task<(AssignmentResponse? Assignment, string? Error)> CreateAsync(CreateAssignmentRequest request, Guid createdBy);
    Task<(bool Success, string? Error)> FulfillAsync(Guid id, FulfillAssignmentRequest request, Guid kurirId);
    Task<(bool Success, string? Error)> CancelAsync(Guid id, Guid userId, string role);
}
