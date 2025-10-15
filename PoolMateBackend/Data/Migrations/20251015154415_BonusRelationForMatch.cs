using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BonusRelationForMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Matches_Player1TpId",
                table: "Matches",
                column: "Player1TpId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Player2TpId",
                table: "Matches",
                column: "Player2TpId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TableId",
                table: "Matches",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_WinnerTpId",
                table: "Matches",
                column: "WinnerTpId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_TournamentPlayers_Player1TpId",
                table: "Matches",
                column: "Player1TpId",
                principalTable: "TournamentPlayers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_TournamentPlayers_Player2TpId",
                table: "Matches",
                column: "Player2TpId",
                principalTable: "TournamentPlayers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_TournamentPlayers_WinnerTpId",
                table: "Matches",
                column: "WinnerTpId",
                principalTable: "TournamentPlayers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_TournamentTables_TableId",
                table: "Matches",
                column: "TableId",
                principalTable: "TournamentTables",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_TournamentPlayers_Player1TpId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_TournamentPlayers_Player2TpId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_TournamentPlayers_WinnerTpId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_TournamentTables_TableId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_Player1TpId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_Player2TpId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_TableId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_WinnerTpId",
                table: "Matches");
        }
    }
}
