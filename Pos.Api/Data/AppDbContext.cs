using Microsoft.EntityFrameworkCore;
using Pos.Api.Data.Enums;
using Pos.Api.Models;

namespace Pos.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPricing> CustomerPricings => Set<CustomerPricing>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<ContainerLoan> ContainerLoans => Set<ContainerLoan>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DeliveryAssignment> DeliveryAssignments => Set<DeliveryAssignment>();
    public DbSet<DeliveryAssignmentItem> DeliveryAssignmentItems => Set<DeliveryAssignmentItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // -- User --------------------------------------------------------------
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.Name).HasMaxLength(100).IsRequired();
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.Role).HasConversion<string>();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        });

        // -- Location ----------------------------------------------------------
        modelBuilder.Entity<Location>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(l => l.Name).HasMaxLength(100).IsRequired();
            e.Property(l => l.Type).HasConversion<string>();
            e.Property(l => l.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(l => l.AssignedUser)
             .WithMany(u => u.AssignedLocations)
             .HasForeignKey(l => l.AssignedTo)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // -- Product -----------------------------------------------------------
        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Category).HasConversion<string>();
            e.Property(p => p.ProductionType).HasConversion<string>();
            e.Property(p => p.Type).HasConversion<string>();
            e.Property(p => p.Unit).HasMaxLength(20).IsRequired();
            e.Property(p => p.BasePrice).HasPrecision(15, 2);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        });

        // -- Customer ----------------------------------------------------------
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Phone).HasMaxLength(20);
            e.Property(c => c.Address).HasMaxLength(500);
            e.Property(c => c.InitialDebt).HasPrecision(15, 2).HasDefaultValue(0m);
            e.Property(c => c.IsConfidential).HasDefaultValue(false);
            e.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        });

        // -- CustomerPricing ---------------------------------------------------
        modelBuilder.Entity<CustomerPricing>(e =>
        {
            e.HasKey(cp => new { cp.CustomerId, cp.ProductId });
            e.Property(cp => cp.CustomPrice).HasPrecision(15, 2);
            e.HasOne(cp => cp.Customer)
             .WithMany(c => c.Pricings)
             .HasForeignKey(cp => cp.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(cp => cp.Product)
             .WithMany(p => p.CustomerPricings)
             .HasForeignKey(cp => cp.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // -- StockMovement -----------------------------------------------------
        modelBuilder.Entity<StockMovement>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.MovementType).HasConversion<string>();
            e.Property(s => s.ContainerStatus).HasConversion<string>();
            e.Property(s => s.PurchaseCost).HasPrecision(15, 2);
            e.Property(s => s.Note).HasMaxLength(255);
            e.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(s => s.Product)
             .WithMany(p => p.StockMovements)
             .HasForeignKey(s => s.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.FromLocation)
             .WithMany(l => l.OutgoingMovements)
             .HasForeignKey(s => s.FromLocationId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(s => s.ToLocation)
             .WithMany(l => l.IncomingMovements)
             .HasForeignKey(s => s.ToLocationId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(s => s.Creator)
             .WithMany(u => u.StockMovements)
             .HasForeignKey(s => s.CreatedBy)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Transaction)
             .WithMany(t => t.StockMovements)
             .HasForeignKey(s => s.TransactionId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(s => s.ContainerLoan)
             .WithMany()
             .HasForeignKey(s => s.ContainerLoanId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // -- Transaction -------------------------------------------------------
        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(t => t.TransactionType).HasConversion<string>();
            e.Property(t => t.Status).HasConversion<string>();
            e.Property(t => t.PaymentMethod).HasConversion<string>();
            e.Property(t => t.TotalAmount).HasPrecision(15, 2);
            e.Property(t => t.PaidAmount).HasPrecision(15, 2);
            e.Property(t => t.DebtAmount).HasPrecision(15, 2);
            e.Property(t => t.Notes).HasMaxLength(500);
            e.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(t => t.Customer)
             .WithMany(c => c.Transactions)
             .HasForeignKey(t => t.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Staff)
             .WithMany(u => u.Transactions)
             .HasForeignKey(t => t.StaffId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Location)
             .WithMany(l => l.Transactions)
             .HasForeignKey(t => t.LocationId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // -- TransactionItem ---------------------------------------------------
        modelBuilder.Entity<TransactionItem>(e =>
        {
            e.HasKey(ti => ti.Id);
            e.Property(ti => ti.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(ti => ti.UnitPrice).HasPrecision(15, 2);
            e.HasOne(ti => ti.Transaction)
             .WithMany(t => t.Items)
             .HasForeignKey(ti => ti.TransactionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ti => ti.Product)
             .WithMany(p => p.TransactionItems)
             .HasForeignKey(ti => ti.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // -- Payment -----------------------------------------------------------
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.Amount).HasPrecision(15, 2);
            e.Property(p => p.Method).HasConversion<string>();
            e.Property(p => p.ReferenceNo).HasMaxLength(100);
            e.Property(p => p.PaidAt).HasDefaultValueSql("now()");
            e.HasOne(p => p.Transaction)
             .WithMany(t => t.Payments)
             .HasForeignKey(p => p.TransactionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // -- DebtPayment -------------------------------------------------------
        modelBuilder.Entity<DebtPayment>(e =>
        {
            e.HasKey(dp => dp.Id);
            e.Property(dp => dp.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(dp => dp.Amount).HasPrecision(15, 2);
            e.Property(dp => dp.Method).HasConversion<string>();
            e.Property(dp => dp.ReferenceNo).HasMaxLength(100);
            e.Property(dp => dp.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(dp => dp.Customer)
             .WithMany(c => c.DebtPayments)
             .HasForeignKey(dp => dp.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(dp => dp.Creator)
             .WithMany(u => u.DebtPayments)
             .HasForeignKey(dp => dp.CreatedBy)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(dp => dp.Transaction)
             .WithMany(t => t.DebtPayments)
             .HasForeignKey(dp => dp.TransactionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // -- ContainerLoan -----------------------------------------------------
        modelBuilder.Entity<ContainerLoan>(e =>
        {
            e.HasKey(cl => cl.Id);
            e.Property(cl => cl.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(cl => cl.IsReversed).HasDefaultValue(false);
            e.Property(cl => cl.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(cl => cl.Transaction)
             .WithMany(t => t.ContainerLoans)
             .HasForeignKey(cl => cl.TransactionId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(cl => cl.Customer)
             .WithMany(c => c.ContainerLoans)
             .HasForeignKey(cl => cl.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(cl => cl.Product)
             .WithMany(p => p.ContainerLoans)
             .HasForeignKey(cl => cl.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(cl => cl.Creator)
             .WithMany(u => u.ContainerLoans)
             .HasForeignKey(cl => cl.CreatedBy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // -- RefreshToken ------------------------------------------------------
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);
            e.Property(rt => rt.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(rt => rt.TokenHash).HasMaxLength(255).IsRequired();
            e.Property(rt => rt.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(rt => rt.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(rt => rt.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // -- DeliveryAssignment ------------------------------------------------
        modelBuilder.Entity<DeliveryAssignment>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(a => a.Status).HasConversion<string>();
            e.Property(a => a.Notes).HasMaxLength(500);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(a => a.Kurir)
             .WithMany(u => u.KurirAssignments)
             .HasForeignKey(a => a.KurirId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Creator)
             .WithMany(u => u.CreatedAssignments)
             .HasForeignKey(a => a.CreatedBy)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Customer)
             .WithMany(c => c.Assignments)
             .HasForeignKey(a => a.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Transaction)
             .WithMany()
             .HasForeignKey(a => a.TransactionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // -- DeliveryAssignmentItem --------------------------------------------
        modelBuilder.Entity<DeliveryAssignmentItem>(e =>
        {
            e.HasKey(ai => ai.Id);
            e.Property(ai => ai.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(ai => ai.UnitPrice).HasPrecision(15, 2);
            e.HasOne(ai => ai.Assignment)
             .WithMany(a => a.Items)
             .HasForeignKey(ai => ai.AssignmentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ai => ai.Product)
             .WithMany(p => p.AssignmentItems)
             .HasForeignKey(ai => ai.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
