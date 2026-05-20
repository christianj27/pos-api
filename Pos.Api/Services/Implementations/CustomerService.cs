using Microsoft.EntityFrameworkCore;
using Pos.Api.Data;
using Pos.Api.DTOs.Customers;
using Pos.Api.Models;
using Pos.Api.Services.Interfaces;

namespace Pos.Api.Services.Implementations;

public class CustomerService(AppDbContext db) : ICustomerService
{
    public async Task<IEnumerable<CustomerResponse>> GetAllAsync(bool activeOnly = false)
    {
        var q = db.Customers.AsQueryable();
        if (activeOnly) q = q.Where(c => c.IsActive);

        var customers = await q.OrderBy(c => c.IsActive ? 0 : 1).ThenBy(c => c.Name).ToListAsync();
        var customerIds = customers.Select(c => c.Id).ToList();

        var totalDebts = await db.Transactions
            .Where(t => customerIds.Contains(t.CustomerId!.Value) && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .GroupBy(t => t.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(t => t.DebtAmount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var totalPaid = await db.DebtPayments
            .Where(dp => customerIds.Contains(dp.CustomerId))
            .GroupBy(dp => dp.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(dp => dp.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        return customers.Select(c =>
        {
            var debt = totalDebts.GetValueOrDefault(c.Id, 0m);
            var paid = totalPaid.GetValueOrDefault(c.Id, 0m);
            return MapToResponse(c, c.InitialDebt + debt - paid);
        });
    }

    public async Task<CustomerResponse?> GetByIdAsync(Guid id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return null;

        var totalDebt = await db.Transactions
            .Where(t => t.CustomerId == id && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .SumAsync(t => t.DebtAmount);
        var totalPaid = await db.DebtPayments
            .Where(dp => dp.CustomerId == id)
            .SumAsync(dp => dp.Amount);

        return MapToResponse(c, c.InitialDebt + totalDebt - totalPaid);
    }

    public async Task<(CustomerResponse? Customer, string? Error)> CreateAsync(CreateCustomerRequest request)
    {
        if (request.InitialDebt.HasValue && request.InitialDebt.Value < 0)
            return (null, "Saldo awal hutang tidak boleh negatif.");

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            InitialDebt = request.InitialDebt ?? 0m
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (MapToResponse(customer, customer.InitialDebt), null);
    }

    public async Task<(CustomerResponse? Customer, string? Error)> UpdateAsync(Guid id, UpdateCustomerRequest request)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return (null, "Customer not found.");

        if (request.InitialDebt.HasValue && request.InitialDebt.Value < 0)
            return (null, "Saldo awal hutang tidak boleh negatif.");

        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone?.Trim();
        customer.Address = request.Address?.Trim();
        customer.IsActive = request.IsActive;
        if (request.InitialDebt.HasValue)
            customer.InitialDebt = request.InitialDebt.Value;
        await db.SaveChangesAsync();

        var totalDebt = await db.Transactions
            .Where(t => t.CustomerId == id && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .SumAsync(t => t.DebtAmount);
        var totalPaid = await db.DebtPayments
            .Where(dp => dp.CustomerId == id)
            .SumAsync(dp => dp.Amount);

        return (MapToResponse(customer, customer.InitialDebt + totalDebt - totalPaid), null);
    }

    public async Task<CustomerPricingResponse?> GetPricingAsync(Guid customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var products = await db.Products.Where(p => p.IsActive).ToListAsync();
        var pricings = await db.CustomerPricings
            .Where(cp => cp.CustomerId == customerId)
            .ToDictionaryAsync(cp => cp.ProductId, cp => cp.CustomPrice);

        var items = products.Select(p => new CustomerPricingItemResponse(
            p.Id, p.Name, p.Unit, p.BasePrice,
            pricings.TryGetValue(p.Id, out var cp) ? cp : null));

        return new CustomerPricingResponse(customerId, customer.Name, items);
    }

    public async Task<(bool Success, string? Error)> UpdatePricingAsync(Guid customerId, UpdateCustomerPricingRequest request)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return (false, "Customer not found.");

        var existing = await db.CustomerPricings
            .Where(cp => cp.CustomerId == customerId)
            .ToListAsync();
        db.CustomerPricings.RemoveRange(existing);

        foreach (var item in request.Items)
        {
            if (item.CustomPrice.HasValue && item.CustomPrice.Value > 0)
            {
                db.CustomerPricings.Add(new CustomerPricing
                {
                    CustomerId = customerId,
                    ProductId = item.ProductId,
                    CustomPrice = item.CustomPrice.Value
                });
            }
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<CustomerDebtSummaryResponse?> GetDebtAsync(Guid customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var totalDebt = await db.Transactions
            .Where(t => t.CustomerId == customerId && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .SumAsync(t => t.DebtAmount);

        var totalPaid = await db.DebtPayments
            .Where(dp => dp.CustomerId == customerId)
            .SumAsync(dp => dp.Amount);

        return new CustomerDebtSummaryResponse(customerId, customer.Name, customer.InitialDebt + totalDebt - totalPaid);
    }

    public async Task<ContainerLoanSummaryResponse?> GetContainerLoansAsync(Guid customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var loans = await db.ContainerLoans
            .Include(cl => cl.Product)
            .Where(cl => cl.CustomerId == customerId)
            .GroupBy(cl => new { cl.ProductId, cl.Product.Name, cl.Product.Unit })
            .Select(g => new ContainerLoanNetItem(g.Key.ProductId, g.Key.Name, g.Key.Unit, g.Sum(cl => cl.Quantity)))
            .Where(item => item.NetQuantity != 0)
            .ToListAsync();

        return new ContainerLoanSummaryResponse(customerId, customer.Name, loans);
    }

    public async Task<CustomerDebtHistoryResponse?> GetDebtHistoryAsync(Guid customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return null;

        var totalDebt = await db.Transactions
            .Where(t => t.CustomerId == customerId && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .SumAsync(t => t.DebtAmount);
        var totalPaid = await db.DebtPayments
            .Where(dp => dp.CustomerId == customerId)
            .SumAsync(dp => dp.Amount);

        var debtTxns = await db.Transactions
            .Include(t => t.Staff)
            .Where(t => t.CustomerId == customerId
                        && t.DebtAmount > 0
                        && t.Status != Data.Enums.TransactionStatus.Cancelled)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new DebtTransactionItem(
                t.Id, t.CreatedAt, t.TransactionType.ToString().ToLower(),
                t.TotalAmount, t.PaidAmount, t.DebtAmount, t.Staff.Name))
            .ToListAsync();

        var payments = await db.DebtPayments
            .Include(dp => dp.Creator)
            .Where(dp => dp.CustomerId == customerId)
            .OrderByDescending(dp => dp.CreatedAt)
            .Select(dp => new DebtPaymentHistoryItem(dp.Id, dp.Amount, dp.Note, dp.Creator.Name, dp.CreatedAt))
            .ToListAsync();

        return new CustomerDebtHistoryResponse(customerId, customer.Name, customer.InitialDebt, customer.InitialDebt + totalDebt - totalPaid, debtTxns, payments);
    }

    private static CustomerResponse MapToResponse(Customer c, decimal outstandingDebt) =>
        new(c.Id, c.Name, c.Phone, c.Address, c.IsActive, c.CreatedAt, outstandingDebt);
}
