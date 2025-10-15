using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolMate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DbForBracketAndMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdvanceToStage2Count",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMultiStage",
                table: "Tournaments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Stage1Ordering",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Stage2Ordering",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TournamentStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    StageNo = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdvanceCount = table.Column<int>(type: "int", nullable: true),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentStages_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: false),
                    Bracket = table.Column<int>(type: "int", nullable: false),
                    RoundNo = table.Column<int>(type: "int", nullable: false),
                    PositionInRound = table.Column<int>(type: "int", nullable: false),
                    Player1TpId = table.Column<int>(type: "int", nullable: true),
                    Player2TpId = table.Column<int>(type: "int", nullable: true),
                    TableId = table.Column<int>(type: "int", nullable: true),
                    ScheduledUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RaceTo = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScoreP1 = table.Column<int>(type: "int", nullable: true),
                    ScoreP2 = table.Column<int>(type: "int", nullable: true),
                    WinnerTpId = table.Column<int>(type: "int", nullable: true),
                    NextWinnerMatchId = table.Column<int>(type: "int", nullable: true),
                    NextLoserMatchId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Matches_NextLoserMatchId",
                        column: x => x.NextLoserMatchId,
                        principalTable: "Matches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Matches_Matches_NextWinnerMatchId",
                        column: x => x.NextWinnerMatchId,
                        principalTable: "Matches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Matches_TournamentStages_StageId",
                        column: x => x.StageId,
                        principalTable: "TournamentStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Matches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tournament_Advance_PowerOfTwo",
                table: "Tournaments",
                sql: "[AdvanceToStage2Count] IS NULL OR ([AdvanceToStage2Count] > 0 AND ([AdvanceToStage2Count] & ([AdvanceToStage2Count]-1)) = 0)");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_NextLoserMatchId",
                table: "Matches",
                column: "NextLoserMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_NextWinnerMatchId",
                table: "Matches",
                column: "NextWinnerMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_StageId_Bracket_RoundNo",
                table: "Matches",
                columns: new[] { "StageId", "Bracket", "RoundNo" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TournamentId_StageId",
                table: "Matches",
                columns: new[] { "TournamentId", "StageId" });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentStages_TournamentId_StageNo",
                table: "TournamentStages",
                columns: new[] { "TournamentId", "StageNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "TournamentStages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tournament_Advance_PowerOfTwo",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "AdvanceToStage2Count",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "IsMultiStage",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Stage1Ordering",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Stage2Ordering",
                table: "Tournaments");
        }
    }
}
