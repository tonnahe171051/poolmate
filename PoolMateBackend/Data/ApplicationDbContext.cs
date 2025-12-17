using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Models;

namespace PoolMate.Api.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // DbSets
        public DbSet<Post> Posts { get; set; }
        public DbSet<Organizer> Organizers { get; set; }

        public DbSet<Venue> Venues => Set<Venue>();
        public DbSet<Tournament> Tournaments => Set<Tournament>();
        public DbSet<PayoutTemplate> PayoutTemplates => Set<PayoutTemplate>();
        public DbSet<Player> Players => Set<Player>();
        public DbSet<TournamentPlayer> TournamentPlayers => Set<TournamentPlayer>();
        public DbSet<TournamentTable> TournamentTables => Set<TournamentTable>();
        public DbSet<TournamentStage> TournamentStages => Set<TournamentStage>();
        public DbSet<Match> Matches => Set<Match>();

        // New method for auto-initialization of DB and schema
        public void EnsureDatabaseCreated(string connectionString)
        {
            // 1. Extract database name
            var builder = new SqlConnectionStringBuilder(connectionString);
            string databaseName = builder.InitialCatalog;

            // 2. Connect to 'master'
            builder.InitialCatalog = "master";
            string masterConnectionString = builder.ToString();

            using (var connection = new SqlConnection(masterConnectionString))
            {
                connection.Open(); // Synchronous Open
                                   // 3. Check and create DB

                var checkDbCommand = $"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{databaseName}') CREATE DATABASE [{databaseName}];";
                using (var command = new SqlCommand(checkDbCommand, connection))
                {
                    command.ExecuteNonQuery(); // Synchronous Execute
                }
            }

            // 4. Run EF Core Migrations synchronously
            Database.Migrate(); // Synchronous Migrate
        }

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

            // ===== Organizer =====
            var org = builder.Entity<Organizer>();
            org.Property(x => x.OrganizationName).HasMaxLength(200).IsRequired();
            org.Property(x => x.Email).HasMaxLength(200).IsRequired();
            org.Property(x => x.FacebookPageUrl).HasMaxLength(300);
            
            // One-to-One relationship with ApplicationUser
            org.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);
            
            // Unique constraint: One user can only have one organizer profile
            org.HasIndex(x => x.UserId).IsUnique();
            
            // Index for email search
            org.HasIndex(x => x.Email);

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
            
            // Configure relationship with Owner
            pt.HasOne(x => x.OwnerUser)
              .WithMany()
              .HasForeignKey(x => x.OwnerUserId)
              .OnDelete(DeleteBehavior.Cascade); // Delete Organizer -> Delete their templates
              
            // Index for faster lookups by owner
            pt.HasIndex(x => x.OwnerUserId);

            // ===== Tournament =====
            var t = builder.Entity<Tournament>();
            t.ToTable(tb =>
            {
                // Check constraint: AdvanceToStage2Count is power of two or null
                tb.HasCheckConstraint(
                    "CK_Tournament_Advance_PowerOfTwo",
                    "[AdvanceToStage2Count] IS NULL OR ([AdvanceToStage2Count] > 0 AND ([AdvanceToStage2Count] & ([AdvanceToStage2Count]-1)) = 0)"
                );
            });

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
            pl.HasIndex(x => x.Slug).IsUnique();
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
            tp.HasIndex(x => new { x.TournamentId, x.PlayerId })
                .IsUnique()
                .HasFilter("[PlayerId] IS NOT NULL");
            tp.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            tp.HasIndex(x => new { x.TournamentId, x.Seed })
                .IsUnique()
                .HasFilter("[Seed] IS NOT NULL")
                .HasDatabaseName("IX_TournamentPlayer_TournamentId_Seed_Unique");

            //index for faster search
            tp.HasIndex(x => x.TournamentId);
            tp.HasIndex(x => new { x.TournamentId, x.Status });
            tp.HasIndex(x => new { x.TournamentId, x.Seed });

            //Tournament table
            var tt = builder.Entity<TournamentTable>();
            tt.Property(x => x.SizeFoot).HasPrecision(3, 1);
            tt.HasIndex(x => x.TournamentId);
            tt.HasOne(x => x.Tournament)
                .WithMany(t => t.Tables)
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            // ------- TournamentStage -------
            var ts = builder.Entity<TournamentStage>();
            ts.HasIndex(x => new { x.TournamentId, x.StageNo }).IsUnique();
            ts.HasOne(x => x.Tournament)
              .WithMany(t => t.Stages)
              .HasForeignKey(x => x.TournamentId)
              .OnDelete(DeleteBehavior.Cascade);

            // ------- Match -------
            var m = builder.Entity<Match>();
            m.HasIndex(x => new { x.TournamentId, x.StageId });
            m.HasIndex(x => new { x.StageId, x.Bracket, x.RoundNo });

            m.HasOne(x => x.Tournament)
             .WithMany(t => t.Matches)
             .HasForeignKey(x => x.TournamentId)
             .OnDelete(DeleteBehavior.NoAction);

            m.HasOne(x => x.Stage)
             .WithMany(s => s.Matches)
             .HasForeignKey(x => x.StageId)
             .OnDelete(DeleteBehavior.Cascade);

            m.HasOne(x => x.Player1Tp).WithMany()
             .HasForeignKey(x => x.Player1TpId).OnDelete(DeleteBehavior.NoAction);
            m.HasOne(x => x.Player2Tp).WithMany()
             .HasForeignKey(x => x.Player2TpId).OnDelete(DeleteBehavior.NoAction);
            m.HasOne(x => x.WinnerTp).WithMany()
             .HasForeignKey(x => x.WinnerTpId).OnDelete(DeleteBehavior.NoAction);

            // FK -> TournamentTables 
            m.HasOne(x => x.Table).WithMany(t => t.Matches)
             .HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.NoAction);

            m.HasIndex(x => x.Player1TpId);
            m.HasIndex(x => x.Player2TpId);
            m.HasIndex(x => x.WinnerTpId);
            m.HasIndex(x => x.TableId);

            m.Property(x => x.Player1SourceType).HasConversion<string>().HasMaxLength(16);
            m.Property(x => x.Player2SourceType).HasConversion<string>().HasMaxLength(16);

            // self refs for next pointers
            m.HasOne<Match>().WithMany().HasForeignKey(x => x.NextWinnerMatchId).OnDelete(DeleteBehavior.NoAction);
            m.HasOne<Match>().WithMany().HasForeignKey(x => x.NextLoserMatchId).OnDelete(DeleteBehavior.NoAction);

            // RowVersion (optimistic concurrency)
            m.Property(x => x.RowVersion).IsRowVersion();
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
