using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.Data.Enums;
using Pos.Api.DTOs.ContainerLoans;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class ContainerLoanService(AppDbContext db) : IContainerLoanService
{
    public async Task<IEnumerable<ContainerLoanResponse>> GetAllAsync(Guid? customerId)
    {
        var q = db.ContainerLoans
            .Include(cl => cl.Customer)
            .Include(cl => cl.Product)
            .Include(cl => cl.Creator)
            .AsQueryable();

        if (customerId.HasValue)
            q = q.Where(cl => cl.CustomerId == customerId.Value);

        return await q
            .OrderByDescending(cl => cl.CreatedAt)
            .Select(cl => new ContainerLoanResponse(
                cl.Id, cl.TransactionId, cl.CustomerId, cl.Customer.Name,
                cl.ProductId, cl.Product.Name, cl.Product.Unit,
                cl.Quantity, cl.Creator.Name, cl.CreatedAt))
            .ToListAsync();
    }

    public async Task<(ContainerLoanResponse? Loan, string? Error)> CreateAsync(
        CreateContainerLoanRequest request, Guid createdBy)
    {
        var product = await db.Products.FindAsync(request.ProductId);
        if (product is null || product.Category != ProductCategory.Refillable)
            return (null, "Hanya produk refillable yang bisa dipinjamkan.");

        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null || !customer.IsActive)
            return (null, "Pelanggan tidak valid.");

        if (request.Quantity == 0)
            return (null, "Jumlah wajib diisi dan tidak boleh nol.");

        var loan = new ContainerLoan
        {
            CustomerId = request.CustomerId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            CreatedBy = createdBy
        };
        db.ContainerLoans.Add(loan);
        await db.SaveChangesAsync();

        var creator = await db.Users.FindAsync(createdBy);
        return (new ContainerLoanResponse(
            loan.Id, null, customer.Id, customer.Name,
            product.Id, product.Name, product.Unit,
            loan.Quantity, creator!.Name, loan.CreatedAt), null);
    }
}
