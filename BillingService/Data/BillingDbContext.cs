using BillingService.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingService.Data;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(invoice => invoice.Id);
            entity.Property(invoice => invoice.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(invoice => invoice.Number).IsUnique();
            entity.Property(invoice => invoice.LastError).HasMaxLength(400);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProductCode).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ProductDescription).HasMaxLength(200).IsRequired();
            entity.HasOne(item => item.Invoice)
                .WithMany(invoice => invoice.Items)
                .HasForeignKey(item => item.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
