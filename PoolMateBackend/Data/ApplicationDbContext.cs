using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // DbSets
        public DbSet<Post> Posts { get; set; }

        public DbSet<Venue> Venues => Set<Venue>();
        public DbSet<Tournament> Tournaments => Set<Tournament>();
        public DbSet<PayoutTemplate> PayoutTemplates => Set<PayoutTemplate>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ===== Post =====
            builder.Entity<Post>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.UserId);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== Venue =====
            var v = builder.Entity<Venue>();
            v.Property(x => x.Name).HasMaxLength(200).IsRequired();
            v.Property(x => x.Address).HasMaxLength(160);
            v.Property(x => x.City).HasMaxLength(100);
            v.Property(x => x.Country).HasMaxLength(2);
            v.HasIndex(x => x.Name);
            v.HasIndex(x => new { x.City, x.Country });
            v.HasOne(x => x.CreatedByUser)
             .WithMany()
             .HasForeignKey(x => x.CreatedByUserId)
             .OnDelete(DeleteBehavior.SetNull);

            // ===== PayoutTemplate =====
            var pt = builder.Entity<PayoutTemplate>();
            pt.Property(x => x.Name).HasMaxLength(200).IsRequired();
            pt.Property(x => x.PercentJson).IsRequired();
            pt.HasIndex(x => new { x.MinPlayers, x.MaxPlayers, x.Places });

            // ===== Tournament =====
            var t = builder.Entity<Tournament>();

            // Decimal precision
            t.Property(x => x.EntryFee).HasColumnType("decimal(12,2)");
            t.Property(x => x.AdminFee).HasColumnType("decimal(12,2)");
            t.Property(x => x.AddedMoney).HasColumnType("decimal(12,2)");
            t.Property(x => x.TotalPrize).HasColumnType("decimal(14,2)");

            // Indexes cho list/filter
            t.HasIndex(x => x.OwnerUserId);
            t.HasIndex(x => x.VenueId);
            t.HasIndex(x => x.IsPublic);
            t.HasIndex(x => x.Status);
            t.HasIndex(x => x.StartUtc);

            // Quan hệ
            t.HasOne(x => x.PayoutTemplate)
             .WithMany()
             .HasForeignKey(x => x.PayoutTemplateId)
             .OnDelete(DeleteBehavior.SetNull);

            t.HasOne(x => x.OwnerUser)
             .WithMany()
             .HasForeignKey(x => x.OwnerUserId)
             .OnDelete(DeleteBehavior.NoAction);

            t.HasOne(x => x.Venue)
             .WithMany(vn => vn.Tournaments)
             .HasForeignKey(x => x.VenueId)
             .OnDelete(DeleteBehavior.SetNull);
        }

        // Update UpdatedAt cho Tournament
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            foreach (var e in ChangeTracker.Entries<Tournament>())
            {
                if (e.State == EntityState.Modified)
                    e.Entity.UpdatedAt = now;
            }

            return base.SaveChangesAsync(ct);
        }
    }
}
