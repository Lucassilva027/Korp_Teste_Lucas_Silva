using Microsoft.EntityFrameworkCore;
using StockService.Models;

namespace StockService.Data;

public sealed class StockDbContext(DbContextOptions<StockDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<AppliedStockOperation> AppliedStockOperations => Set<AppliedStockOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Code).HasMaxLength(50).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(200).IsRequired();
            entity.HasIndex(product => product.Code).IsUnique();
        });

        modelBuilder.Entity<AppliedStockOperation>(entity =>
        {
            entity.HasKey(operation => operation.Id);
            entity.Property(operation => operation.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(operation => operation.InvoiceNumber).HasMaxLength(30);
            entity.HasIndex(operation => operation.OperationKey).IsUnique();
        });
    }
}
