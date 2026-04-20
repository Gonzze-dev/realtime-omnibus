using Microsoft.EntityFrameworkCore;
using RealTime.Models;

namespace RealTime.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id");
            e.Property(n => n.GroupKey).HasColumnName("group_key");
            e.Property(n => n.GroupName).HasColumnName("group_name");
            e.Property(n => n.Expiration).HasColumnName("expiration");
            e.Property(n => n.Date).HasColumnName("date");
            e.Property(n => n.Payload).HasColumnName("payload").HasColumnType("jsonb");
        });
    }
}
