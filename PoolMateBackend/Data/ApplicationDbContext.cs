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
        public DbSet<Player> Players => Set<Player>();
        public DbSet<TournamentPlayer> TournamentPlayers => Set<TournamentPlayer>();

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

            // Player
            var pl = builder.Entity<Player>();
            pl.Property(x => x.Country).HasMaxLength(2);
            pl.HasIndex(x => x.FullName);
            pl.HasOne(x => x.User)
              .WithMany()
              .HasForeignKey(x => x.UserId)
              .OnDelete(DeleteBehavior.NoAction);

            // TournamentPlayer
            var tp = builder.Entity<TournamentPlayer>();
            tp.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            tp.Property(x => x.Country).HasMaxLength(2);
            tp.HasOne(tp => tp.Tournament)
                .WithMany(t => t.TournamentPlayers)
                .HasForeignKey(tp => tp.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            tp.HasOne(tp => tp.Player)
                .WithMany(p => p.TournamentPlayers)
                .HasForeignKey(tp => tp.PlayerId)
                .OnDelete(DeleteBehavior.SetNull);
            tp.HasIndex(x => new {x.TournamentId, x.PlayerId})
                .IsUnique()
                .HasFilter("[PlayerId] IS NOT NULL");
            tp.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);

            //index for faster search
            tp.HasIndex(x => x.TournamentId);                         
            tp.HasIndex(x => new { x.TournamentId, x.Status });       
            tp.HasIndex(x => new { x.TournamentId, x.Seed });         


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
