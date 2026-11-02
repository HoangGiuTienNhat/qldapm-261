using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Domain;

namespace Restaurant.Api.Data;
public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<StaffSession> StaffSessions => Set<StaffSession>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<GuestSession> GuestSessions => Set<GuestSession>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderBatch> OrderBatches => Set<OrderBatch>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Role>().HasKey(x => x.Code);
        m.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleCode });
        m.Entity<UserRole>().HasOne<StaffUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<UserRole>().HasOne<Role>().WithMany().HasForeignKey(x=>x.RoleCode).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StaffSession>().HasOne<StaffUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<MenuItem>().HasOne<MenuCategory>().WithMany().HasForeignKey(x=>x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Visit>().HasOne<RestaurantTable>().WithMany().HasForeignKey(x=>x.TableId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Visit>().HasOne<StaffUser>().WithMany().HasForeignKey(x=>x.OpenedByStaffId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<GuestSession>().HasOne<Visit>().WithMany().HasForeignKey(x=>x.VisitId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Order>().HasOne<Visit>().WithMany().HasForeignKey(x=>x.VisitId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Order>().HasOne<StaffUser>().WithMany().HasForeignKey(x=>x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderBatch>().HasOne<Order>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderBatch>().HasOne<StaffUser>().WithMany().HasForeignKey(x=>x.SubmittedByStaffId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderBatch>().HasOne<GuestSession>().WithMany().HasForeignKey(x=>x.SubmittedByGuestSessionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderItem>().HasOne<OrderBatch>().WithMany().HasForeignKey(x=>x.BatchId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderItem>().HasOne<MenuItem>().WithMany().HasForeignKey(x=>x.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Payment>().HasOne<Order>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Payment>().HasOne<StaffUser>().WithMany().HasForeignKey(x=>x.ReceivedByStaffId).OnDelete(DeleteBehavior.Restrict);
        foreach (var entity in m.Model.GetEntityTypes())
        {
            var table = entity.ClrType.Name switch
            {
                nameof(MenuCategory) => "menu_categories", nameof(RestaurantTable) => "restaurant_tables",
                nameof(OrderBatch) => "order_batches",
                _ => Snake(entity.ClrType.Name) + "s"
            };
            m.Entity(entity.ClrType).ToTable(table);
            foreach (var p in entity.GetProperties()) p.SetColumnName(Snake(p.Name));
            if (typeof(Mutable).IsAssignableFrom(entity.ClrType))
                m.Entity(entity.ClrType).Property(nameof(Mutable.Version)).IsConcurrencyToken();
        }
        m.Entity<AuditEvent>().Property(x => x.BeforeData).HasColumnType("jsonb");
        m.Entity<AuditEvent>().Property(x => x.AfterData).HasColumnType("jsonb");
    }
    private static string Snake(string value) => Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
