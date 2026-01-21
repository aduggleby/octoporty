// OctoportyDbContext.cs
// Entity Framework Core DbContext for the Agent's SQL Server database.
// Defines PortMappings, ConnectionLogs, and RequestLogs with their relationships.
// Enforces unique constraint on ExternalDomain to prevent duplicate mappings.

using Microsoft.EntityFrameworkCore;
using Octoporty.Shared.Entities;

namespace Octoporty.Agent.Data;

public class OctoportyDbContext : DbContext
{
    public OctoportyDbContext(DbContextOptions<OctoportyDbContext> options) : base(options)
    {
    }

    public DbSet<PortMapping> PortMappings => Set<PortMapping>();
    public DbSet<ConnectionLog> ConnectionLogs => Set<ConnectionLog>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PortMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExternalDomain).HasMaxLength(255).IsRequired();
            entity.Property(e => e.InternalHost).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.ExternalDomain).IsUnique();
        });

        modelBuilder.Entity<ConnectionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientIp).HasMaxLength(45);
            entity.Property(e => e.DisconnectReason).HasMaxLength(500);
            entity.HasOne(e => e.PortMapping)
                .WithMany()
                .HasForeignKey(e => e.PortMappingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ConnectedAt);
        });

        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Method).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Path).HasMaxLength(2048).IsRequired();
            entity.Property(e => e.QueryString).HasMaxLength(2048);
            entity.Property(e => e.ClientIp).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.HasOne(e => e.PortMapping)
                .WithMany()
                .HasForeignKey(e => e.PortMappingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ConnectionLog)
                .WithMany()
                .HasForeignKey(e => e.ConnectionLogId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
