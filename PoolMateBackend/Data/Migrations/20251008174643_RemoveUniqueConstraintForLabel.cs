using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueConstraintForLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TournamentTables_TournamentId_Label",
                table: "TournamentTables");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTables_TournamentId",
                table: "TournamentTables",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TournamentTables_TournamentId",
                table: "TournamentTables");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTables_TournamentId_Label",
                table: "TournamentTables",
                columns: new[] { "TournamentId", "Label" },
                unique: true);
        }
    }
}
