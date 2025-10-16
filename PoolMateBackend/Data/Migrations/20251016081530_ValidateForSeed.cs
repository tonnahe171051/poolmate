using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ValidateForSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TournamentPlayers_TournamentId_Seed",
                table: "TournamentPlayers");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayer_TournamentId_Seed_Unique",
                table: "TournamentPlayers",
                columns: new[] { "TournamentId", "Seed" },
                unique: true,
                filter: "[Seed] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TournamentPlayer_TournamentId_Seed_Unique",
                table: "TournamentPlayers");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_TournamentId_Seed",
                table: "TournamentPlayers",
                columns: new[] { "TournamentId", "Seed" });
        }
    }
}
