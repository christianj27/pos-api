using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.Assignments;
using Pos.Api.DTOs.Transactions;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class AssignmentService(AppDbContext db, ITransactionService transactionService) : IAssignmentService
{
    public async Task<IEnumerable<AssignmentResponse>> GetAllAsync(Guid userId, string role, DateOnly? date = null)
    {
        var q = db.DeliveryAssignments
            .Include(a => a.Kurir)
            .Include(a => a.Customer)
            .Include(a => a.Location)
            .Include(a => a.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        if (role == "kurir")
            q = q.Where(a => a.KurirId == userId);

        if (date.HasValue)
        {
            var (start, end) = WibTimeZone.GetUtcDayBounds(date.Value);
            q = q.Where(a => a.CreatedAt >= start && a.CreatedAt < end);
        }

        return await q
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => MapToResponse(a))
            .ToListAsync();
    }

    public async Task<(AssignmentResponse? Assignment, string? Error)> CreateAsync(
        CreateAssignmentRequest request, Guid createdBy)
    {
        var kurir = await db.Users.FindAsync(request.KurirId);
        if (kurir is null || !kurir.IsActive || kurir.Role != UserRole.Kurir)
            return (null, "Kurir tidak valid.");

        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null || !customer.IsActive)
            return (null, "Pelanggan tidak valid.");

        var location = await db.Locations.FindAsync(request.LocationId);
        if (location is null || !location.IsActive)
            return (null, "Lokasi tidak valid atau tidak aktif.");

        var assignment = new DeliveryAssignment
        {
            KurirId = request.KurirId,
            CustomerId = request.CustomerId,
            LocationId = request.LocationId,
            CreatedBy = createdBy,
            Notes = request.Notes,
            Status = AssignmentStatus.Pending
        };
        db.DeliveryAssignments.Add(assignment);
        await db.SaveChangesAsync();

        foreach (var item in request.Items)
        {
            db.DeliveryAssignmentItems.Add(new DeliveryAssignmentItem
            {
                AssignmentId = assignment.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }
        await db.SaveChangesAsync();

        var created = await db.DeliveryAssignments
            .Include(a => a.Kurir).Include(a => a.Customer).Include(a => a.Location)
            .Include(a => a.Items).ThenInclude(i => i.Product)
            .FirstAsync(a => a.Id == assignment.Id);

        return (MapToResponse(created), null);
    }

    public async Task<(bool Success, string? Error)> FulfillAsync(
        Guid id, FulfillAssignmentRequest request, Guid kurirId)
    {
        var assignment = await db.DeliveryAssignments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment is null) return (false, "Penugasan tidak ditemukan.");
        if (assignment.Status != AssignmentStatus.Pending) return (false, "Penugasan ini sudah diproses atau dibatalkan.");
        if (assignment.KurirId != kurirId) return (false, "Anda tidak memiliki izin untuk memproses penugasan ini.");

        // Use location stored on assignment; fall back to kurir's assigned vehicle for old records
        Guid locationId;
        if (assignment.LocationId.HasValue)
        {
            locationId = assignment.LocationId.Value;
        }
        else
        {
            var vehicle = await db.Locations
                .FirstOrDefaultAsync(l => l.AssignedTo == kurirId && l.Type == LocationType.Vehicle && l.IsActive);
            if (vehicle is null) return (false, "Kendaraan untuk kurir ini tidak ditemukan.");
            locationId = vehicle.Id;
        }

        var txRequest = new CreateTransactionRequest(
            TransactionType: "delivery",
            CustomerId: assignment.CustomerId,
            LocationId: locationId,
            Items: (request.Items is not null && request.Items.Any())
                ? request.Items.Select(i => new TransactionItemRequest(i.ProductId, i.Quantity, i.UnitPrice))
                : assignment.Items.Select(i => new TransactionItemRequest(i.ProductId, i.Quantity, i.UnitPrice)),
            PaidAmount: request.PaidAmount,
            PaymentMethod: request.PaymentMethod,
            Notes: request.Notes ?? assignment.Notes,
            ContainerReturns: request.ContainerReturns?.Select(r => new ContainerReturnRequest(r.ProductId, r.Quantity)),
            DebtPaymentAmount: request.DebtPaymentAmount
        );

        var (txDetail, error) = await transactionService.CreateAsync(txRequest, kurirId, "kurir");
        if (txDetail is null) return (false, error);

        assignment.Status = AssignmentStatus.Fulfilled;
        assignment.TransactionId = txDetail.Id;
        await db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CancelAsync(Guid id, Guid userId, string role)
    {
        var assignment = await db.DeliveryAssignments.FindAsync(id);
        if (assignment is null) return (false, "Penugasan tidak ditemukan.");
        if (assignment.Status != AssignmentStatus.Pending) return (false, "Hanya penugasan yang menunggu bisa dibatalkan.");

        assignment.Status = AssignmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return (true, null);
    }

    private static AssignmentResponse MapToResponse(DeliveryAssignment a) =>
        new(a.Id, a.Status.ToString().ToLower(), a.KurirId, a.Kurir.Name,
            a.CustomerId, a.Customer.Name, a.LocationId, a.Location?.Name,
            a.Notes, a.TransactionId, a.CreatedAt,
            a.Items.Select(i => new AssignmentItemResponse(
                i.ProductId, i.Product.Name, i.Product.Unit, i.Quantity, i.UnitPrice)));
}
